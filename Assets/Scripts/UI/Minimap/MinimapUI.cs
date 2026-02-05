using System.Collections.Generic;
using UnityEngine;
using Geneforge.Gameplay.Map;

namespace Geneforge.UI.Minimap
{
    /// <summary>
    /// Minimap UI that displays room icons and player position.
    /// Uses actual world positions for accurate representation.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        public static MinimapUI Instance { get; private set; }

        [Header("Prefabs & Containers")]
        [SerializeField] private GameObject roomIconPrefab;
        [SerializeField] private RectTransform iconsContainer;
        [SerializeField] private RectTransform playerMarker; 

        [Header("Scale & Smoothing")]
        [Tooltip("How many UI pixels per world unit. Higher = more zoomed in.")]
        [SerializeField] private float worldToUIScale = 3f;
        [SerializeField] private float smoothTime = 0.2f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        [Header("Room Icons (Fallback)")]
        [SerializeField] private Sprite iconHub;
        [SerializeField] private Sprite iconCombat;
        [SerializeField] private Sprite iconShop;
        [SerializeField] private Sprite iconBoss;

        private Dictionary<System.Guid, MinimapRoomIcon> spawnedIcons = new Dictionary<System.Guid, MinimapRoomIcon>();
        private RoomInstance currentRoom;
        private Transform playerTransform;
        private Vector2 currentVelocity;
        
        // Is the player marker incorrectly parented inside the container?
        private bool playerMarkerIsChildOfContainer;

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
            // Check hierarchy setup
            if (playerMarker != null && iconsContainer != null)
            {
                playerMarkerIsChildOfContainer = playerMarker.IsChildOf(iconsContainer);
                if (playerMarkerIsChildOfContainer)
                {
                    Debug.LogWarning("[MinimapUI] PlayerMarker is a child of IconsContainer! " +
                        "This is not recommended but will be compensated for. " +
                        "Move PlayerMarker to be a sibling of IconsContainer for best results.");
                }
            }
            
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
            // Get player reference
            if (playerTransform == null && DungeonMapManager.Instance != null)
                playerTransform = DungeonMapManager.Instance.Player;

            if (playerTransform == null || playerMarker == null) return;
            if (DungeonMapManager.Instance == null || DungeonMapManager.Instance.CurrentHub == null) return;

            // Ensure player marker renders on top
            playerMarker.SetAsLastSibling();

            // Get hub position as world origin
            Vector3 hubWorldPos = DungeonMapManager.Instance.CurrentHub.transform.position;

            // Convert player's world position to minimap position relative to hub
            Vector3 playerWorldPos = playerTransform.position;
            Vector3 playerWorldOffset = playerWorldPos - hubWorldPos;
            Vector2 playerMinimapPos = WorldToMinimap(playerWorldOffset);

            // Move the icons container so the player stays at screen center
            // Container moves OPPOSITE to player position
            Vector2 targetContainerPos = -playerMinimapPos;
            iconsContainer.anchoredPosition = Vector2.SmoothDamp(
                iconsContainer.anchoredPosition, 
                targetContainerPos, 
                ref currentVelocity, 
                smoothTime
            );

            // Position player marker
            if (playerMarkerIsChildOfContainer)
            {
                // If player marker is inside the container, we need to place it at the player's minimap position
                // (since the container moves, the marker stays in place relative to the world)
                playerMarker.anchoredPosition = playerMinimapPos;
            }
            else
            {
                // If player marker is outside the container, it stays at center (0,0)
                playerMarker.anchoredPosition = Vector2.zero;
            }
            
            // Rotate player marker to match player facing
            playerMarker.localRotation = Quaternion.Euler(0, 0, -playerTransform.eulerAngles.y);

            // Debug logging
            if (showDebugLogs && Time.frameCount % 60 == 0) // Log once per second
            {
                Debug.Log($"[MinimapUI] Player world: {playerWorldPos}, Hub world: {hubWorldPos}, " +
                    $"Offset: {playerWorldOffset}, MinimapPos: {playerMinimapPos}, " +
                    $"Container: {iconsContainer.anchoredPosition}");
            }
        }

        /// <summary>
        /// Converts a world-space offset (from hub origin) to minimap UI position.
        /// X-axis maps to UI X, Z-axis maps to UI Y.
        /// </summary>
        private Vector2 WorldToMinimap(Vector3 worldOffset)
        {
            return new Vector2(worldOffset.x * worldToUIScale, worldOffset.z * worldToUIScale);
        }

        public void RefreshMap()
        {
            if (DungeonMapManager.Instance == null) return;
            
            if (DungeonMapManager.Instance.CurrentHub != null) 
                HandleRoomDiscovered(DungeonMapManager.Instance.CurrentHub);
            
            foreach (var kvp in DungeonMapManager.Instance.RoomsByDirection)
                if (kvp.Value != null) 
                    HandleRoomDiscovered(kvp.Value);
        }

        private void HandleRoomDiscovered(RoomInstance room)
        {
            if (room == null || spawnedIcons.ContainsKey(room.RoomGuid)) return;
            
            GameObject iconGO = Instantiate(roomIconPrefab, iconsContainer);
            iconGO.name = $"Icon_{room.DirectionFromHub}_{room.RoomType}"; 
            
            MinimapRoomIcon icon = iconGO.GetComponent<MinimapRoomIcon>();
            RectTransform rect = iconGO.GetComponent<RectTransform>();
            
            // Get the ACTUAL world position of this room
            Vector3 hubWorldPos = DungeonMapManager.Instance.CurrentHub.transform.position;
            Vector3 roomWorldPos = room.transform.position;
            Vector3 roomWorldOffset = roomWorldPos - hubWorldPos;
            
            // Convert world offset to minimap position
            Vector2 iconPosition = WorldToMinimap(roomWorldOffset);
            
            // Apply rotation for diagonal rooms
            float iconRotation = 0f;
            if (room.RoomType != RoomType.Hub && room.DirectionFromHub.IsDiagonal())
            {
                iconRotation = GetMinimapRotationForDirection(room.DirectionFromHub);
            }
            
            rect.anchoredPosition = iconPosition;
            rect.localRotation = Quaternion.Euler(0, 0, iconRotation);

            icon.Initialize(room);
            spawnedIcons[room.RoomGuid] = icon;
            UpdateIconStates();
            
            if (showDebugLogs)
            {
                Debug.Log($"[MinimapUI] Room '{room.name}' at world {roomWorldPos}, " +
                    $"offset {roomWorldOffset} -> minimap {iconPosition}");
            }
        }
        
        /// <summary>
        /// Returns the Z-axis rotation for minimap icons based on room direction.
        /// </summary>
        private float GetMinimapRotationForDirection(RoomDirection dir)
        {
            switch (dir)
            {
                case RoomDirection.SouthEast: return 180f;
                case RoomDirection.SouthWest: return 90f;
                case RoomDirection.NorthWest: return 0f;
                case RoomDirection.NorthEast: return -90f;
                default: return 0f;
            }
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
