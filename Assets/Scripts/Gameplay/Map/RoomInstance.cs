using System;
using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    /// <summary>
    /// Attached to all room prefabs (hubs, combat, shops, etc).
    /// Also coordinates enemy & reward spawning for combat rooms.
    /// </summary>
    public class RoomInstance : MonoBehaviour
    {
        [Header("Static (per prefab)")]
        public RoomType roomType = RoomType.Combat;

        [Header("Encounter Setup (combat rooms)")]
        [Tooltip("If true, entering this room's trigger will start the encounter.")]
        public bool autoStartEncounterOnEnter = true;

        [Tooltip("Enemy spawners belonging to this room (will be auto-filled from children if empty).")]
        public EnemySpawner[] enemySpawners;

        [Tooltip("Reward spawners in this room (auto-filled from children if empty).")]
        public RewardSpawner[] rewardSpawners;

        [Header("Runtime (filled by DungeonMapManager)")]
        public TimelineId timelineId;
        public int floorIndex;
        public RoomDirection directionFromHub;
        public int visitOrderGlobal = -1;
        public int visitOrderAmongDiagonals = -1;
        public bool isKeyRoom;

        public Guid RoomGuid { get; private set; }

        // Encounter state
        private bool encounterStarted;
        private int enemiesAlive;

        private void Awake()
        {
            RoomGuid = Guid.NewGuid();

            if (enemySpawners == null || enemySpawners.Length == 0)
                enemySpawners = GetComponentsInChildren<EnemySpawner>(true);

            if (rewardSpawners == null || rewardSpawners.Length == 0)
                rewardSpawners = GetComponentsInChildren<RewardSpawner>(true);
        }

        public void Initialize(TimelineId timeline, int floor, RoomDirection dir, RoomType type)
        {
            timelineId = timeline;
            floorIndex = floor;
            directionFromHub = dir;
            roomType = type;
        }

        /// <summary>
        /// Called by DungeonMapManager once this room is chosen as the key room.
        /// Picks exactly one RewardSpawner in this room to hold the key.
        /// </summary>
        public void MarkAsKeyRoom()
        {
            isKeyRoom = true;
            if (rewardSpawners == null || rewardSpawners.Length == 0)
            {
                Debug.LogWarning($"[RoomInstance] Key room {name} has no RewardSpawners.");
                return;
            }

            int idx = UnityEngine.Random.Range(0, rewardSpawners.Length);
            rewardSpawners[idx].ConfigureKeySpawn(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            // 1) Room visit tracking / key logic
            if (DungeonMapManager.Instance != null)
            {
                DungeonMapManager.Instance.HandleRoomEntered(this);
            }

            // 2) Start encounter when player enters the room
            if (autoStartEncounterOnEnter && roomType == RoomType.Combat)
            {
                StartEncounterIfNeeded();
            }
        }

        /// <summary>
        /// Called from OnTriggerEnter or manually if you need scripted starts.
        /// </summary>
        public void StartEncounterIfNeeded()
        {
            if (encounterStarted) return;
            encounterStarted = true;

            enemiesAlive = 0;

            if (enemySpawners != null)
            {
                foreach (var spawner in enemySpawners)
                {
                    if (spawner == null) continue;
                    spawner.Initialize(this);
                    enemiesAlive += spawner.SpawnEnemies();
                }
            }

            // If nothing was spawned, consider the room instantly cleared.
            if (enemiesAlive <= 0)
            {
                OnAllEnemiesCleared();
            }
        }

        /// <summary>
        /// Called by EnemySpawner whenever one of its enemies dies.
        /// </summary>
        internal void NotifyEnemyDied()
        {
            enemiesAlive--;
            if (enemiesAlive <= 0)
            {
                OnAllEnemiesCleared();
            }
        }

        private void OnAllEnemiesCleared()
        {
            if (rewardSpawners == null) return;

            foreach (var rs in rewardSpawners)
            {
                if (rs == null) continue;
                rs.SpawnRewards();
            }
        }
    }
}
