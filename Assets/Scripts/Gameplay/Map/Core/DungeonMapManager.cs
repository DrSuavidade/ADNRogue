using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Geneforge.Gameplay.Progression;
using Geneforge.Gameplay.Items;

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

        public float CurrentStatMultiplier
        {
            get
            {
                if (dungeonConfig == null) return 1f;
                var timelineSet = dungeonConfig.GetTimeline(currentTimeline);
                return timelineSet != null ? timelineSet.statPickupMultiplier : 1f;
            }
        }

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

            // Determine timeline:
            // 1. If we are in a proper run (RunSession Active), trust the Persistence Manager.
            // 2. If we are debug testing (Editor, no run start), use the Inspector 'startingTimeline'.
            if (RunSession.Instance != null && RunSession.Instance.IsRunActive)
            {
                currentTimeline = RunPersistenceManager.Instance.CurrentTimeline;
            }
            else
            {
                currentTimeline = startingTimeline;
                // Update persistence so other systems know we are pretending to be in this timeline
                if (RunPersistenceManager.Instance != null)
                   RunPersistenceManager.Instance.CurrentTimeline = currentTimeline;
            }

            TimelineRoomSet set = dungeonConfig != null ? dungeonConfig.GetTimeline(currentTimeline) : null;
            if (set == null)
            {
                return;
            }

            var items = GetAvailableItemsForCurrentTimeline();
            Debug.Log($"[DungeonMapManager] Floor {currentFloorIndex} in {currentTimeline} generated. Available reward items: {items.Count}");

            floorsInThisTimeline = overrideFloors > 0 ? overrideFloors : Mathf.Max(1, set.floors);
            currentFloorIndex = 0;
            GenerateFloor();
        }


        #region Generation

        private void GenerateFloor()
        {
            StartCoroutine(GenerateFloorRoutine());
        }

        private System.Collections.IEnumerator GenerateFloorRoutine()
        {
            ClearCurrentFloor();

            TimelineRoomSet set = dungeonConfig.GetTimeline(currentTimeline);
            if (set == null || set.hubPrefab == null)
            {
                yield break;
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
            GameObject hubGO;
            if (Geneforge.Core.Pooling.PoolManager.Instance != null)
                hubGO = Geneforge.Core.Pooling.PoolManager.Instance.Spawn(set.hubPrefab, Vector3.zero, Quaternion.identity);
            else
                hubGO = Instantiate(set.hubPrefab, Vector3.zero, Quaternion.identity);

            currentHub = hubGO.GetComponent<HubRoom>();
            if (currentHub == null)
            {
                yield break;
            }
            currentHub.Initialize(currentTimeline, currentFloorIndex, RoomDirection.South, RoomType.Hub);

            // Yield after heavy hub instantiation
            yield return null;

            // Notify Minimap of Hub Discovery
            if (MinimapManager.Instance != null)
            {
                MinimapManager.Instance.ReportRoomDiscovery(currentHub);
            }

            if (player != null && currentHub.SouthEntrySpawn != null)
            {
                player.position = currentHub.SouthEntrySpawn.position;
                player.rotation = currentHub.SouthEntrySpawn.rotation;
            }

            // Hub is visited immediately
            if (MinimapManager.Instance != null)
            {
                MinimapManager.Instance.ReportRoomVisit(currentHub);
            }

            // Diagonal combat rooms
            if (set.combatRoomsSE == null || set.combatRoomsSE.Count == 0)
            {
                Debug.LogError("DungeonMapManager: No combatRoomsSE configured for timeline " + currentTimeline);
            }
            else
            {
                SpawnDiagonalCombatRoom(RoomDirection.NorthEast, set);
                yield return null; // Spread spawning over frames
                SpawnDiagonalCombatRoom(RoomDirection.SouthEast, set);
                yield return null;
                SpawnDiagonalCombatRoom(RoomDirection.SouthWest, set);
                yield return null;
                SpawnDiagonalCombatRoom(RoomDirection.NorthWest, set);
                yield return null;
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

            // Get the hub's tunnel for this direction
            Transform hubTunnel = currentHub.GetTunnelForDirection(dir);
            
            // Calculate rotation for the room (from SE base orientation to target direction)
            Quaternion rot = dir.RotationFromSE();

            // Spawn the room (from pool or instantiate)
            GameObject roomGO;
            if (Geneforge.Core.Pooling.PoolManager.Instance != null)
                roomGO = Geneforge.Core.Pooling.PoolManager.Instance.Spawn(prefab, Vector3.zero, rot);
            else
                roomGO = Instantiate(prefab, Vector3.zero, rot);

            RoomInstance room = roomGO.GetComponent<RoomInstance>();
            if (room == null)
            {
                // If it was pooled, we should reclaim it, but for simplicity:
                Destroy(roomGO);
                return;
            }

            // Calculate position based on tunnel alignment
            Vector3 worldPos;
            if (hubTunnel != null && room.TunnelR != null)
            {
                // Calculate offset
                Vector3 tunnelRWorldOffset = room.TunnelR.position - roomGO.transform.position;
                worldPos = hubTunnel.position - tunnelRWorldOffset;
                room.TunnelR.gameObject.SetActive(false);
            }
            else
            {
                Vector2Int offset = dir.ToGridOffset();
                worldPos = currentHub.transform.position + new Vector3(offset.x, 0f, offset.y) * roomSpacing;
            }

            roomGO.transform.position = worldPos;
            room.Initialize(currentTimeline, currentFloorIndex, dir, RoomType.Combat);
            currentRoomsByDirection[dir] = room;

            // Notify Minimap
            if (MinimapManager.Instance != null)
            {
                MinimapManager.Instance.ReportRoomDiscovery(room);
            }
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
                if (Geneforge.Core.Pooling.PoolManager.Instance != null)
                    Geneforge.Core.Pooling.PoolManager.Instance.Reclaim(currentHub.gameObject);
                else
                    Destroy(currentHub.gameObject);
                
                currentHub = null;
            }

            foreach (var kvp in currentRoomsByDirection)
            {
                if (kvp.Value != null)
                {
                    if (Geneforge.Core.Pooling.PoolManager.Instance != null)
                        Geneforge.Core.Pooling.PoolManager.Instance.Reclaim(kvp.Value.gameObject);
                    else
                        Destroy(kvp.Value.gameObject);
                }
            }
            currentRoomsByDirection.Clear();

            currentFloorRewardPool = null;
            currentFloorEnemyPool = null;
            currentFloorKeyPrefab = null;

            if (MinimapManager.Instance != null)
            {
                MinimapManager.Instance.ClearData();
            }
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

            // Notify Minimap
            if (MinimapManager.Instance != null)
            {
                MinimapManager.Instance.ReportRoomVisit(room);
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

        /// <summary>
        /// Returns a list of items from the global pool that are allowed in the current timeline 
        /// based on their rarity (progression logic).
        /// </summary>
        public List<Geneforge.Gameplay.Items.RewardItemData> GetAvailableItemsForCurrentTimeline()
        {
            if (dungeonConfig == null || dungeonConfig.GlobalRewardItemPool == null) return new List<Geneforge.Gameplay.Items.RewardItemData>();

            List<Geneforge.Gameplay.Items.RewardItemData> filtered = new List<Geneforge.Gameplay.Items.RewardItemData>();
            
            // Progression logic:
            // Prehistoric (0) -> Common, Rare
            // Roman (1)       -> Common, Rare, Epic
            // Present (2)     -> Common, Rare, Epic, Legendary
            // Future (3)      -> Common, Rare, Epic, Legendary, Mythic

            int timelineIndex = (int)currentTimeline;

            foreach (var item in dungeonConfig.GlobalRewardItemPool)
            {
                if (item == null) continue;

                bool allowed = false;
                switch (item.Rarity)
                {
                    case ItemRarity.Common:
                    case ItemRarity.Rare:
                        allowed = true; // Always allowed from world 1
                        break;
                    case ItemRarity.Epic:
                        if (timelineIndex >= 1) allowed = true;
                        break;
                    case ItemRarity.Legendary:
                        if (timelineIndex >= 2) allowed = true;
                        break;
                    case ItemRarity.Mythic:
                        if (timelineIndex >= 3) allowed = true;
                        break;
                }

                if (allowed)
                    filtered.Add(item);
            }

            return filtered;
        }

        /// <summary>
        /// Returns N random items from the global pool using the rarity weights 
        /// defined for the current timeline in DungeonConfig.
        /// </summary>
        public List<RewardItemData> GetWeightedRandomRewardItems(int count)
        {
            if (dungeonConfig == null || dungeonConfig.GlobalRewardItemPool == null) 
                return new List<RewardItemData>();

            var set = dungeonConfig.GetTimeline(currentTimeline);
            if (set == null) return new List<RewardItemData>();

            // Group available items by rarity for faster picking
            Dictionary<ItemRarity, List<RewardItemData>> itemsByRarity = new Dictionary<ItemRarity, List<RewardItemData>>();
            foreach (var item in dungeonConfig.GlobalRewardItemPool)
            {
                if (item == null) continue;
                if (!itemsByRarity.ContainsKey(item.Rarity))
                    itemsByRarity[item.Rarity] = new List<RewardItemData>();
                itemsByRarity[item.Rarity].Add(item);
            }

            List<RewardItemData> result = new List<RewardItemData>();
            int attempts = 0;
            int maxAttempts = 100; // Prevent infinite loop

            // Try to pick 'count' unique items
            while (result.Count < count && attempts < maxAttempts)
            {
                attempts++;
                float totalWeight = set.commonRate + set.rareRate + set.epicRate + set.legendaryRate + set.mythicRate;
                if (totalWeight <= 0) break;

                float roll = UnityEngine.Random.Range(0, totalWeight);
                ItemRarity selectedRarity = ItemRarity.Common;

                if (roll < set.commonRate) selectedRarity = ItemRarity.Common;
                else if (roll < set.commonRate + set.rareRate) selectedRarity = ItemRarity.Rare;
                else if (roll < set.commonRate + set.rareRate + set.epicRate) selectedRarity = ItemRarity.Epic;
                else if (roll < set.commonRate + set.rareRate + set.epicRate + set.legendaryRate) selectedRarity = ItemRarity.Legendary;
                else selectedRarity = ItemRarity.Mythic;

                if (itemsByRarity.ContainsKey(selectedRarity) && itemsByRarity[selectedRarity].Count > 0)
                {
                    var pool = itemsByRarity[selectedRarity];
                    var pickedItem = pool[UnityEngine.Random.Range(0, pool.Count)];
                    if (!result.Contains(pickedItem)) result.Add(pickedItem);
                }
            }

            return result;
        }

        public RewardItemData GetRewardItemByName(string itemName)
        {
            if (dungeonConfig == null || dungeonConfig.GlobalRewardItemPool == null) return null;
            return dungeonConfig.GlobalRewardItemPool.Find(i => i != null && i.ItemName == itemName);
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
