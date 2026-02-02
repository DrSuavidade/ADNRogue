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

        [Header("Layout Settings")]
        [SerializeField] private float spacing = 100f;
        [Tooltip("Use this to manually align the map under the player icon.")]
        [SerializeField] private Vector2 mapCenterOffset;

        [Header("Room Icons (Backup)")]
        [SerializeField] private Sprite iconHub;
        [SerializeField] private Sprite iconCombat;
        [SerializeField] private Sprite iconShop;
        [SerializeField] private Sprite iconBoss;

        private Dictionary<System.Guid, MinimapRoomIcon> spawnedIcons = new Dictionary<System.Guid, MinimapRoomIcon>();
        private RoomInstance currentRoom;
        private Transform playerTransform;

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

            if (playerTransform != null)
            {
                // 1. O MAPA É ESTÁTICO: O iconsContainer não roda.
                iconsContainer.localRotation = Quaternion.identity;

                // 2. ROTAÇÃO DO PLAYER (Mostra direção)
                if (playerMarker != null)
                {
                    playerMarker.localRotation = Quaternion.Euler(0, 0, -playerTransform.eulerAngles.y);
                }

                if (playerMarker != null)
                {
                    playerMarker.SetAsLastSibling();
                }

                // 3. POSICIONAMENTO FINAL
                if (playerMarker != null && DungeonMapManager.Instance != null && DungeonMapManager.Instance.CurrentHub != null)
                {
                    // --- GARANTIR QUE O PLAYER ESTÁ SEMPRE POR CIMA ---
                    // Se o playerMarker e o iconsContainer estiverem no mesmo pai (ex: dentro da Mask),
                    // o playerMarker tem de ser o último para ser desenhado por cima.
                    playerMarker.SetAsLastSibling();

                    float worldUnitsPerRoom = DungeonMapManager.Instance.RoomSpacing;
                    float scale = spacing / worldUnitsPerRoom;
                    
                    Vector3 hubPos = DungeonMapManager.Instance.CurrentHub.transform.position;
                    Vector3 worldPosRelToHub = playerTransform.position - hubPos;

                    // --- DETECÇÃO AUTOMÁTICA DE SAÍDA DE SALA ---
                    if (currentRoom != null)
                    {
                        Vector2Int gridPos = currentRoom.DirectionFromHub.ToGridOffset();
                        Vector3 roomWorldCenter = hubPos + new Vector3(gridPos.x, 0, gridPos.y) * worldUnitsPerRoom;
                        float dist = Vector3.Distance(playerTransform.position, roomWorldCenter);
                        
                        // Se estivermos longe demais do centro, saímos do modo snap
                        if (dist > worldUnitsPerRoom * 0.55f) 
                        {
                            currentRoom = null;
                        }
                    }

                    if (currentRoom != null)
                    {
                        // --- MODO SALA (ESTÁTICO TOTAL) ---
                        // 1. O ícone do jogador fica "colado" no centro absoluto da máscara
                        playerMarker.anchoredPosition = Vector2.zero;

                        // 2. O mapa faz snap para que a sala fique exatamente sob o ícone do player
                        Vector2Int gridPos = currentRoom.DirectionFromHub.ToGridOffset();
                        Vector2 roomCenterMinimapPos = new Vector2(gridPos.x * spacing, gridPos.y * spacing);
                        iconsContainer.anchoredPosition = -roomCenterMinimapPos + mapCenterOffset;
                    }
                    else
                    {
                        // --- MODO CORREDOR (POSIÇÃO LIVRE) ---
                        // 1. O mapa segue a posição real do jogador para manter o player no centro
                        Vector2 playerPosOnMap = new Vector2(worldPosRelToHub.x * scale, worldPosRelToHub.z * scale);
                        iconsContainer.anchoredPosition = -playerPosOnMap + mapCenterOffset;

                        // 2. O ícone do jogador fica no centro
                        playerMarker.anchoredPosition = Vector2.zero;
                    }
                }
            }
        }

        public void RefreshMap()
        {
            Debug.Log("[MinimapUI] RefreshMap called");
            
            if (DungeonMapManager.Instance == null)
            {
                Debug.LogWarning("[MinimapUI] DungeonMapManager.Instance is NULL!");
                return;
            }

            // 1. Force Discovery of Hub
            if (DungeonMapManager.Instance.CurrentHub != null)
            {
                Debug.Log("[MinimapUI] Found Hub, calling HandleRoomDiscovered");
                HandleRoomDiscovered(DungeonMapManager.Instance.CurrentHub);
            }
            else
            {
                Debug.LogWarning("[MinimapUI] CurrentHub is NULL!");
            }

            // 2. Force Discovery of all generated diagonal rooms
            Debug.Log($"[MinimapUI] Found {DungeonMapManager.Instance.RoomsByDirection.Count} diagonal rooms");
            foreach (var kvp in DungeonMapManager.Instance.RoomsByDirection)
                if (kvp.Value != null) HandleRoomDiscovered(kvp.Value);
        }

        private void HandleRoomDiscovered(RoomInstance room)
        {
            if (room == null || spawnedIcons.ContainsKey(room.RoomGuid)) return;
            
            if (iconsContainer.Find($"Icon_{room.DirectionFromHub}_{room.RoomType}")) return;

            GameObject iconGO = Instantiate(roomIconPrefab, iconsContainer);
            iconGO.name = $"Icon_{room.DirectionFromHub}_{room.RoomType}"; 
            
            // Ordem: Hub atrás, Combate à frente, Player sempre no topo (na hierarquia do painel)
            if (room.RoomType == RoomType.Hub) iconGO.transform.SetAsFirstSibling();
            else iconGO.transform.SetAsLastSibling();

            MinimapRoomIcon icon = iconGO.GetComponent<MinimapRoomIcon>();
            RectTransform rect = iconGO.GetComponent<RectTransform>();
            
            // FORÇAR TAMANHO (Caso o prefab esteja a 0,0)
            if (rect.sizeDelta.x < 5) rect.sizeDelta = new Vector2(spacing, spacing);
            
            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero; 
            
            // POSICIONAMENTO
            if (room.RoomType != RoomType.Hub)
            {
                Vector2Int gridPos = room.DirectionFromHub.ToGridOffset();
                rect.anchoredPosition = new Vector2(gridPos.x * spacing, gridPos.y * spacing);
            }
            else
            {
                rect.anchoredPosition = Vector2.zero;
            }

            icon.Initialize(room);
            spawnedIcons[room.RoomGuid] = icon;
            UpdateIconStates();
        }

        private void HandleRoomVisited(RoomInstance room)
        {
            currentRoom = room;
            UpdateIconStates();

            // SNAP DO PLAYER PARA A SALA ATIVA
            if (playerMarker != null && spawnedIcons.ContainsKey(room.RoomGuid))
            {
                MinimapRoomIcon roomIcon = spawnedIcons[room.RoomGuid];
                playerMarker.anchoredPosition = roomIcon.rectTransform.anchoredPosition;
            }
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
