using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Geneforge.Gameplay.Map
{
    /// <summary>
    /// Central procedural map generator & runtime state tracker.
    /// One instance per scene / timeline.
    /// </summary>
    public class DungeonMapManager : MonoBehaviour
    {
        public static DungeonMapManager Instance { get; private set; }

        [Header("Config")]
        public DungeonConfig dungeonConfig;
        public TimelineId startingTimeline = TimelineId.Prehistoric;
        [Tooltip("Optional override, leave -1 to use config.floors for this timeline.")]
        public int overrideFloors = -1;

        [Header("Runtime references")]
        public Transform player;               // Assign existing player in scene.
        public UnityEvent onBossStairsUsed;    // Hook to scene navigation / boss scene, per timeline.

        [Header("Layout")]
        [Tooltip("World-space distance between hub center and each adjacent room.")]
        public float roomSpacing = 50f;

        // Current run state
        private TimelineId currentTimeline;
        private int currentFloorIndex; // 0-based within timeline
        private int floorsInThisTimeline;

        private HubRoom currentHub;
        private readonly Dictionary<RoomDirection, RoomInstance> currentRoomsByDirection =
            new Dictionary<RoomDirection, RoomInstance>();

        // Visit / key state for current floor
        private int globalVisitCounter;
        private int diagonalVisitCounter;
        private int keyWillAppearOnDiagonalVisitIndex; // 2, 3, or 4
        private RoomInstance keyRoom;
        private bool playerHasKey;

        #region Unity lifecycle

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
            currentTimeline = startingTimeline;
            TimelineRoomSet set = dungeonConfig != null ? dungeonConfig.GetTimeline(currentTimeline) : null;
            if (set == null)
            {
                Debug.LogError("DungeonMapManager: No DungeonConfig / TimelineRoomSet assigned.");
                return;
            }

            floorsInThisTimeline = overrideFloors > 0 ? overrideFloors : Mathf.Max(1, set.floors);
            currentFloorIndex = 0;
            GenerateFloor();
        }

        #endregion

        #region Generation

        private void GenerateFloor()
        {
            ClearCurrentFloor();

            TimelineRoomSet set = dungeonConfig.GetTimeline(currentTimeline);
            if (set == null || set.hubPrefab == null)
            {
                Debug.LogError("DungeonMapManager: TimelineRoomSet or hubPrefab missing.");
                return;
            }

            globalVisitCounter = 0;
            diagonalVisitCounter = 0;
            keyRoom = null;
            playerHasKey = false;
            // Pre-roll key visit index: 2, 3, or 4
            keyWillAppearOnDiagonalVisitIndex = Random.Range(2, 5);

            // 1. Spawn hub
            GameObject hubGO = Instantiate(set.hubPrefab, Vector3.zero, Quaternion.identity);
            currentHub = hubGO.GetComponent<HubRoom>();
            if (currentHub == null)
            {
                Debug.LogError("DungeonMapManager: Hub prefab must have a HubRoom component.");
                return;
            }
            currentHub.Initialize(currentTimeline, currentFloorIndex, RoomDirection.South, RoomType.Hub); // hub is 'entered from south'

            // Drop player at hub's south entry
            if (player != null && currentHub.southEntrySpawn != null)
            {
                player.position = currentHub.southEntrySpawn.position;
                player.rotation = currentHub.southEntrySpawn.rotation;
            }

            // 2. Spawn diagonal combat rooms (NE, SE, SW, NW)
            if (set.combatRoomsSE == null || set.combatRoomsSE.Count == 0)
            {
                Debug.LogError("DungeonMapManager: No combatRoomsSE configured for timeline " + currentTimeline);
            }
            else
            {
                SpawnDiagonalCombatRoom(RoomDirection.NorthEast, set);
                SpawnDiagonalCombatRoom(RoomDirection.SouthEast, set);
                SpawnDiagonalCombatRoom(RoomDirection.SouthWest, set);
                SpawnDiagonalCombatRoom(RoomDirection.NorthWest, set);
            }

            // NOTE: We intentionally do NOT spawn East / West rooms yet,
            // but they are fully supported by TimelineRoomSet (shops/events) for future expansion.

            // 3. Configure north exit in hub (stairs to boss or next floor)
            ConfigureNorthExit();
        }

        private void SpawnDiagonalCombatRoom(RoomDirection dir, TimelineRoomSet set)
        {
            if (!dir.IsDiagonal()) return;

            GameObject prefab = set.combatRoomsSE[Random.Range(0, set.combatRoomsSE.Count)];
            Vector2Int offset = dir.ToGridOffset();
            Vector3 worldPos = currentHub.transform.position + new Vector3(offset.x, 0f, offset.y) * roomSpacing;
            Quaternion rot = dir.RotationFromSE();

            GameObject roomGO = Instantiate(prefab, worldPos, rot);
            RoomInstance room = roomGO.GetComponent<RoomInstance>();
            if (room == null)
            {
                Debug.LogError("DungeonMapManager: Combat room prefab must have a RoomInstance component.");
                return;
            }

            room.Initialize(currentTimeline, currentFloorIndex, dir, RoomType.Combat);
            currentRoomsByDirection[dir] = room;
        }

        private void ConfigureNorthExit()
        {
            if (currentHub == null) return;

            bool lastFloor = (currentFloorIndex >= floorsInThisTimeline - 1);
            string label = lastFloor ? "Boss stairs" : "Stairs to next floor";
            Debug.Log($"[DungeonMapManager] North exit on floor {currentFloorIndex + 1}/{floorsInThisTimeline} configured as: {label}");

            // The actual stairs prefab / visual can be part of the hub prefab.
            // Gate behaviour (requiring the key) is handled by NorthExitGate.
        }

        private void ClearCurrentFloor()
        {
            if (currentHub != null)
            {
                Destroy(currentHub.gameObject);
                currentHub = null;
            }

            foreach (var kvp in currentRoomsByDirection)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            currentRoomsByDirection.Clear();
        }

        #endregion

        #region Visits & key placement

        /// <summary>
        /// Called by RoomInstance whenever the player enters its trigger.
        /// Handles visit order tracking and key placement rules.
        /// </summary>
        public void HandleRoomEntered(RoomInstance room)
        {
            if (room == null) return;

            // Only count first visit to each room for ordering.
            if (room.visitOrderGlobal < 0)
            {
                globalVisitCounter++;
                room.visitOrderGlobal = globalVisitCounter;
            }

            // Only diagonal combat rooms participate in key logic.
            if (room.roomType == RoomType.Combat && room.directionFromHub.IsDiagonal())
            {
                if (room.visitOrderAmongDiagonals < 0)
                {
                    diagonalVisitCounter++;
                    room.visitOrderAmongDiagonals = diagonalVisitCounter;

                    // First diagonal visited can never hold the key.
                    if (diagonalVisitCounter == keyWillAppearOnDiagonalVisitIndex && keyRoom == null)
                    {
                        keyRoom = room;
                        room.MarkAsKeyRoom();
                        Debug.Log($"[DungeonMapManager] Key assigned to room {room.directionFromHub} (diagonal visit #{diagonalVisitCounter}).");
                    }
                }
            }
        }

        public void NotifyPlayerPickedUpKey()
        {
            playerHasKey = true;
            Debug.Log("[DungeonMapManager] Player picked up floor key.");
        }

        #endregion

        #region North exit usage

        /// <summary>
        /// Called by NorthExitGate when the player tries to go through the north exit.
        /// </summary>
        public void TryUseNorthExit(NorthExitGate gate)
        {
            if (!playerHasKey)
            {
                Debug.Log("[DungeonMapManager] North exit is locked. Player has no key.");
                gate?.OnUseDenied();
                return;
            }

            // Consume key for this floor
            playerHasKey = false;

            bool lastFloor = (currentFloorIndex >= floorsInThisTimeline - 1);
            if (lastFloor)
            {
                Debug.Log("[DungeonMapManager] Using north exit: this is the boss stairs.");
                gate?.OnUseAcceptedBoss();
                onBossStairsUsed?.Invoke();
                // Timeline transition / boss scene is handled externally via UnityEvent.
            }
            else
            {
                Debug.Log("[DungeonMapManager] Using north exit: going to next floor.");
                gate?.OnUseAcceptedNextFloor();
                currentFloorIndex++;
                GenerateFloor();
            }
        }

        #endregion

        #region Query API (for minimap / debugging)

        public IReadOnlyDictionary<RoomDirection, RoomInstance> RoomsByDirection => currentRoomsByDirection;
        public HubRoom CurrentHub => currentHub;
        public int CurrentFloorIndex => currentFloorIndex;
        public int FloorsInThisTimeline => floorsInThisTimeline;
        public TimelineId CurrentTimeline => currentTimeline;
        public RoomInstance KeyRoom => keyRoom;

        #endregion
    }
}
