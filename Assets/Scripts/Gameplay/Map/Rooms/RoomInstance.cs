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
        [SerializeField] private RoomType roomType = RoomType.Combat;

        [Header("Encounter Setup (combat rooms)")]
        [SerializeField] private bool autoStartEncounterOnEnter = true;

        [Header("Scene children / hooks")]
        [SerializeField] private EnemySpawner[] enemySpawners;
        [SerializeField] private RewardSpawner[] rewardSpawners;

        [Header("Runtime state (debug)")]
        [SerializeField] private TimelineId timelineId;
        [SerializeField] private int floorIndex;
        [SerializeField] private RoomDirection directionFromHub;
        [SerializeField] private int visitOrderGlobal = -1;
        [SerializeField] private int visitOrderAmongDiagonals = -1;
        [SerializeField] private bool isKeyRoom;

        public Guid RoomGuid { get; private set; }


        // Encounter state
        private bool encounterStarted;
        private int enemiesAlive;

        #region Query API

        public RoomType RoomType => roomType;
        public TimelineId TimelineId => timelineId;
        public int FloorIndex => floorIndex;
        public RoomDirection DirectionFromHub => directionFromHub;
        public int VisitOrderGlobal { get => visitOrderGlobal; set => visitOrderGlobal = value; }
        public int VisitOrderAmongDiagonals { get => visitOrderAmongDiagonals; set => visitOrderAmongDiagonals = value; }
        public bool IsKeyRoom => isKeyRoom;
        public bool EncounterStarted => encounterStarted;
        public bool AutoStartEncounterOnEnter => autoStartEncounterOnEnter;
        public EnemySpawner[] EnemySpawners => enemySpawners;
        public RewardSpawner[] RewardSpawners => rewardSpawners;


        #endregion


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
            OnPlayerEnteredRoom();
        }

        /// <summary>
        /// Hook for subclasses (e.g. shops, events) to customize what happens when the player enters.
        /// Base implementation handles visit tracking and auto-starting combat encounters.
        /// </summary>
        protected virtual void OnPlayerEnteredRoom()
        {
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
            Debug.Log($"[RoomInstance] Enemy died in {name}, enemiesAlive now = {enemiesAlive}");

            if (enemiesAlive <= 0)
            {
                Debug.Log($"[RoomInstance] All enemies cleared in {name}, spawning rewards.");
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
