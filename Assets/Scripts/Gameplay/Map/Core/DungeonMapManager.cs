using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Map
{
    public class DungeonMapManager : MonoBehaviour
    {
        public static DungeonMapManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private DungeonConfig dungeonConfig;
        [SerializeField] private TimelineId startingTimeline = TimelineId.Prehistoric;
        [Tooltip("Optional override, leave -1 to use config.floors for this timeline.")]
        [SerializeField] private int overrideFloors = -1;

        [Header("Runtime references")]
        [SerializeField] private Transform player;
        [SerializeField] private UnityEvent onBossStairsUsed;

        [Header("Layout")]
        [Tooltip("World-space distance between hub center and each adjacent room.")]
        [SerializeField] private float roomSpacing = 50f;

        [Header("Rewards")]
        [Tooltip("Fallback key prefab if TimelineRoomSet.keyPickupPrefab is not set.")]
        [SerializeField] private GameObject defaultKeyPickupPrefab;


        private TimelineId currentTimeline;
        private int currentFloorIndex;
        private int floorsInThisTimeline;

        private HubRoom currentHub;
        private readonly Dictionary<RoomDirection, RoomInstance> currentRoomsByDirection =
            new Dictionary<RoomDirection, RoomInstance>();

        private List<WeightedPrefab> currentFloorRewardPool;
        private List<WeightedPrefab> currentFloorEnemyPool;
        private GameObject currentFloorKeyPrefab;
        private int globalVisitCounter;
        private int diagonalVisitCounter;
        private int keyWillAppearOnDiagonalVisitIndex;
        private RoomInstance keyRoom;
        private bool playerHasKey;
        public event Action<bool> KeyStateChanged;
        public bool PlayerHasKey => playerHasKey;
        private void RaiseKeyStateChanged() => KeyStateChanged?.Invoke(playerHasKey);

        #region Query API

        public IReadOnlyDictionary<RoomDirection, RoomInstance> RoomsByDirection => currentRoomsByDirection;
        public HubRoom CurrentHub => currentHub;
        public int CurrentFloorIndex => currentFloorIndex;
        public int FloorsInThisTimeline => floorsInThisTimeline;
        public TimelineId CurrentTimeline => currentTimeline;
        public RoomInstance KeyRoom => keyRoom;
        public DungeonConfig DungeonConfig => dungeonConfig;
        public TimelineId StartingTimeline => startingTimeline;
        public int OverrideFloors => overrideFloors;
        public Transform Player => player;
        public UnityEvent OnBossStairsUsed => onBossStairsUsed;
        public float RoomSpacing => roomSpacing;
        public GameObject DefaultKeyPickupPrefab => defaultKeyPickupPrefab;

        #endregion


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
            if (dungeonConfig == null) return;

            if (RunState.HasTimelineOverride)
            {
                currentTimeline = RunState.CurrentTimeline;
            }
            else
            {
                currentTimeline = startingTimeline;
                RunState.CurrentTimeline = currentTimeline;
                RunState.HasTimelineOverride = true;
            }

            TimelineRoomSet set = dungeonConfig != null ? dungeonConfig.GetTimeline(currentTimeline) : null;
            if (set == null)
            {
                return;
            }

            floorsInThisTimeline = overrideFloors > 0 ? overrideFloors : Mathf.Max(1, set.floors);
            currentFloorIndex = 0;
            GenerateFloor();
        }


        #region Generation

        private void GenerateFloor()
        {
            ClearCurrentFloor();

            TimelineRoomSet set = dungeonConfig.GetTimeline(currentTimeline);
            if (set == null || set.hubPrefab == null)
            {
                return;
            }

            // Per-floor pools
            currentFloorRewardPool = set.floorRewardPrefabs;
            currentFloorEnemyPool = set.enemyPrefabs;
            currentFloorKeyPrefab = set.keyPickupPrefab != null
                ? set.keyPickupPrefab
                : defaultKeyPickupPrefab;

            if (currentFloorKeyPrefab == null)
            {
                Debug.LogWarning($"[DungeonMapManager] No key prefab configured for timeline {currentTimeline}. Key will NOT spawn!");
            }

            globalVisitCounter = 0;
            diagonalVisitCounter = 0;
            keyRoom = null;
            playerHasKey = false;
            keyWillAppearOnDiagonalVisitIndex = UnityEngine.Random.Range(2, 5); // 2..4

            RaiseKeyStateChanged();

            // Hub
            GameObject hubGO = Instantiate(set.hubPrefab, Vector3.zero, Quaternion.identity);
            currentHub = hubGO.GetComponent<HubRoom>();
            if (currentHub == null)
            {
                return;
            }
            currentHub.Initialize(currentTimeline, currentFloorIndex, RoomDirection.South, RoomType.Hub);

            if (player != null && currentHub.SouthEntrySpawn != null)
            {
                player.position = currentHub.SouthEntrySpawn.position;
                player.rotation = currentHub.SouthEntrySpawn.rotation;
            }

            // Diagonal combat rooms
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

            ConfigureNorthExit();
        }

        private void SpawnDiagonalCombatRoom(RoomDirection dir, TimelineRoomSet set)
        {
            if (!dir.IsDiagonal()) return;

            GameObject prefab = ChooseWeightedPrefab(set.combatRoomsSE);
            if (prefab == null)
            {
                return;
            }

            Vector2Int offset = dir.ToGridOffset();
            Vector3 worldPos = currentHub.transform.position + new Vector3(offset.x, 0f, offset.y) * roomSpacing;
            Quaternion rot = dir.RotationFromSE();

            GameObject roomGO = Instantiate(prefab, worldPos, rot);
            RoomInstance room = roomGO.GetComponent<RoomInstance>();
            if (room == null)
            {
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

            currentFloorRewardPool = null;
            currentFloorEnemyPool = null;
            currentFloorKeyPrefab = null;
        }

        #endregion

        #region Visits & key placement

        public void HandleRoomEntered(RoomInstance room)
        {
            if (room == null) return;

            if (room.VisitOrderGlobal < 0)
            {
                globalVisitCounter++;
                room.VisitOrderGlobal = globalVisitCounter;
            }

            if (room.RoomType == RoomType.Combat && room.DirectionFromHub.IsDiagonal())
            {
                if (room.VisitOrderAmongDiagonals < 0)
                {
                    diagonalVisitCounter++;
                    room.VisitOrderAmongDiagonals = diagonalVisitCounter;

                    if (diagonalVisitCounter == keyWillAppearOnDiagonalVisitIndex && keyRoom == null)
                    {
                        keyRoom = room;
                        room.MarkAsKeyRoom();
                        Debug.Log($"[DungeonMapManager] Key assigned to room {room.DirectionFromHub} (diagonal visit #{diagonalVisitCounter}).");
                    }
                }
            }
        }

        public void NotifyPlayerPickedUpKey()
        {
            playerHasKey = true;
            RaiseKeyStateChanged();
        }

        #endregion

        #region Reward & enemy API

        public void SpawnKeyAt(RewardSpawner spawner)
        {
            if (spawner == null) return;

            if (currentFloorKeyPrefab == null)
            {
                return;
            }

            Instantiate(currentFloorKeyPrefab, spawner.transform.position, spawner.transform.rotation);
        }

        public void SpawnRewardAt(RewardSpawner spawner)
        {
            if (spawner == null) return;

            GameObject prefab = ChooseWeightedPrefab(currentFloorRewardPool);
            if (prefab == null)
            {
                return;
            }

            Instantiate(prefab, spawner.transform.position, spawner.transform.rotation);
        }

        public GameObject GetRandomEnemyPrefab()
        {
            return ChooseWeightedPrefab(currentFloorEnemyPool);
        }

        private GameObject ChooseWeightedPrefab(List<WeightedPrefab> list)
        {
            if (list == null || list.Count == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].prefab == null) continue;
                if (list[i].weight <= 0f) continue;
                totalWeight += list[i].weight;
            }

            if (totalWeight <= 0f) return null;

            float r = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry.prefab == null || entry.weight <= 0f) continue;

                r -= entry.weight;
                if (r <= 0f)
                    return entry.prefab;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].prefab != null) return list[i].prefab;
            }
            return null;
        }

        #endregion

        #region North exit usage

        public void TryUseNorthExit(NorthExitGate gate)
        {
            if (!playerHasKey)
            {
                gate?.OnUseDenied();
                return;
            }

            playerHasKey = false;
            RaiseKeyStateChanged();

            bool lastFloor = (currentFloorIndex >= floorsInThisTimeline - 1);
            if (lastFloor)
            {
                gate?.OnUseAcceptedBoss();
                onBossStairsUsed?.Invoke();
            }
            else
            {
                gate?.OnUseAcceptedNextFloor();
                currentFloorIndex++;
                GenerateFloor();
            }
        }

        #endregion
    }
}
