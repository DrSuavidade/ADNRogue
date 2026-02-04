using System.Collections.Generic;
using UnityEngine;
using Geneforge.Gameplay.Map;

namespace Geneforge.UI.Minimap
{
    public class MinimapUI : MonoBehaviour
    {
        public static MinimapUI Instance { get; private set; }

        [Header("Prefabs & Containers")]
        [SerializeField] private GameObject roomIconPrefab;
        [SerializeField] private RectTransform iconsContainer;
        [SerializeField] private RectTransform playerMarker; 

        [Header("Zoom & Calibration")]
        [SerializeField] private float spacing = 150f;
        [SerializeField] private float normalScale = 1.0f;     
        [SerializeField] private float roomZoomScale = 3.0f;   
        [SerializeField] private float smoothTime = 0.2f;
        
        [Tooltip("Calibração: Quanto mede a 'área verde' do desenho em metros do jogo? Aumenta este valor se o player sair do desenho cedo demais.")]
        [SerializeField] private float roomVisualSize = 25f;   

        [Header("Room Icons (Backup)")]
        [SerializeField] private Sprite iconHub;
        [SerializeField] private Sprite iconCombat;
        [SerializeField] private Sprite iconShop;
        [SerializeField] private Sprite iconBoss;

        private Dictionary<System.Guid, MinimapRoomIcon> spawnedIcons = new Dictionary<System.Guid, MinimapRoomIcon>();
        private RoomInstance currentRoom;
        private Transform playerTransform;
        private Vector2 currentVelocity;
        
        private bool isFocusMode = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (MinimapManager.Instance != null)
            {
                MinimapManager.Instance.RoomDiscovered += HandleRoomDiscovered;
                MinimapManager.Instance.RoomVisited += HandleRoomVisited;
            }
            RefreshMap();
        }

        private void OnDestroy()
        {
            if (MinimapManager.Instance != null)
            {
                MinimapManager.Instance.RoomDiscovered -= HandleRoomDiscovered;
                MinimapManager.Instance.RoomVisited -= HandleRoomVisited;
            }
        }

        private void Update()
        {
            if (playerTransform == null && DungeonMapManager.Instance != null)
                playerTransform = DungeonMapManager.Instance.Player;

            if (playerTransform != null && playerMarker != null && DungeonMapManager.Instance != null && DungeonMapManager.Instance.CurrentHub != null)
            {
                playerMarker.SetAsLastSibling();

                float worldSpacing = DungeonMapManager.Instance.RoomSpacing;
                float normalMapScale = spacing / worldSpacing; 
                Vector3 hubPos = DungeonMapManager.Instance.CurrentHub.transform.position;

                UpdateRoomDetection(hubPos, worldSpacing);

                Vector2 targetContainerPos;
                Vector2 targetPlayerIconPos;
                float targetScale;

                if (isFocusMode && currentRoom != null)
                {
                    targetScale = roomZoomScale;
                    Vector2Int gridPos = currentRoom.DirectionFromHub.ToGridOffset();
                    Vector2 roomCenterMapPos = new Vector2(gridPos.x * spacing, gridPos.y * spacing);
                    targetContainerPos = -roomCenterMapPos; 

                    Vector3 roomWorldPos = hubPos + new Vector3(gridPos.x, 0, gridPos.y) * worldSpacing;
                    Vector3 playerRelToRoom = playerTransform.position - roomWorldPos;

                    float zoomPixelFactor = (spacing / roomVisualSize); 
                    targetPlayerIconPos = new Vector2(playerRelToRoom.x * zoomPixelFactor * roomZoomScale, playerRelToRoom.z * zoomPixelFactor * roomZoomScale);

                    float maxRadius = 100f; 
                    targetPlayerIconPos = Vector2.ClampMagnitude(targetPlayerIconPos, maxRadius);
                }
                else
                {
                    targetScale = normalScale;
                    Vector3 relWorldPos = playerTransform.position - hubPos;
                    Vector2 playerMapPos = new Vector2(relWorldPos.x * normalMapScale, relWorldPos.z * normalMapScale);
                    targetContainerPos = -playerMapPos;
                    targetPlayerIconPos = Vector2.zero;
                }

                iconsContainer.anchoredPosition = Vector2.SmoothDamp(iconsContainer.anchoredPosition, targetContainerPos, ref currentVelocity, smoothTime);
                
                float currentScale = iconsContainer.localScale.x;
                float newScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * 5f);
                iconsContainer.localScale = new Vector3(newScale, newScale, 1f);

                playerMarker.anchoredPosition = targetPlayerIconPos;
                playerMarker.localRotation = Quaternion.Euler(0, 0, -playerTransform.eulerAngles.y);
            }
        }

        private void UpdateRoomDetection(Vector3 hubPos, float worldSpacing)
        {
            if (isFocusMode && currentRoom != null)
            {
                Vector2Int gridPos = currentRoom.DirectionFromHub.ToGridOffset();
                Vector3 roomCenter = hubPos + new Vector3(gridPos.x, 0, gridPos.y) * worldSpacing;
                
                if (Vector3.Distance(playerTransform.position, roomCenter) > roomVisualSize * 0.7f) 
                {
                    isFocusMode = false;
                    currentRoom = null;
                }
            }
            else
            {
                if (CheckRoomEntry(DungeonMapManager.Instance.CurrentHub, hubPos, worldSpacing)) return;

                foreach (var kvp in DungeonMapManager.Instance.RoomsByDirection)
                {
                    if (CheckRoomEntry(kvp.Value, hubPos, worldSpacing)) return;
                }
            }
        }

        private bool CheckRoomEntry(RoomInstance room, Vector3 hubPos, float worldSpacing)
        {
            if (room == null) return false;
            Vector2Int gridPos = room.DirectionFromHub.ToGridOffset();
            Vector3 roomCenter = hubPos + new Vector3(gridPos.x, 0, gridPos.y) * worldSpacing;

            if (Vector3.Distance(playerTransform.position, roomCenter) < roomVisualSize * 0.5f)
            {
                currentRoom = room;
                isFocusMode = true;
                return true;
            }
            return false;
        }

        public void RefreshMap()
        {
            if (DungeonMapManager.Instance == null) return;
            if (DungeonMapManager.Instance.CurrentHub != null) HandleRoomDiscovered(DungeonMapManager.Instance.CurrentHub);
            foreach (var kvp in DungeonMapManager.Instance.RoomsByDirection)
                if (kvp.Value != null) HandleRoomDiscovered(kvp.Value);
        }

        private void HandleRoomDiscovered(RoomInstance room)
        {
            if (room == null || spawnedIcons.ContainsKey(room.RoomGuid)) return;
            
            GameObject iconGO = Instantiate(roomIconPrefab, iconsContainer);
            iconGO.name = $"Icon_{room.DirectionFromHub}_{room.RoomType}"; 
            
            MinimapRoomIcon icon = iconGO.GetComponent<MinimapRoomIcon>();
            RectTransform rect = iconGO.GetComponent<RectTransform>();
            
            Vector2Int gridPos = room.DirectionFromHub.ToGridOffset();
            rect.anchoredPosition = new Vector2(gridPos.x * spacing, gridPos.y * spacing);

            icon.Initialize(room);
            spawnedIcons[room.RoomGuid] = icon;
            UpdateIconStates();
        }

        private void HandleRoomVisited(RoomInstance room)
        {
            currentRoom = room;
            UpdateIconStates();
        }

        private void UpdateIconStates()
        {
            foreach (var kvp in spawnedIcons)
            {
                bool visited = MinimapManager.Instance.IsRoomVisited(kvp.Key);
                bool active = (currentRoom != null && currentRoom.RoomGuid == kvp.Key);
                kvp.Value.SetState(visited, active);
            }
        }

        public Sprite GetIconForType(RoomType type)
        {
            switch (type)
            {
                case RoomType.Hub: return iconHub;
                case RoomType.Combat: return iconCombat;
                case RoomType.Shop: return iconShop;
                default: return iconCombat;
            }
        }
    }
}
