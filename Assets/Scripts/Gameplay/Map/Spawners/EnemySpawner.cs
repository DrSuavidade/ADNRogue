using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("How many enemies to spawn from the global enemy pool.")]
        [SerializeField] private int spawnCount = 1;

        [Tooltip("Points to spawn enemies at; if fewer than spawnCount, they will wrap around.")]
        [SerializeField] private Transform[] spawnPoints;

        public int SpawnCount
        {
            get => spawnCount;
            set => spawnCount = Mathf.Max(0, value);
        }

        public Transform[] SpawnPoints => spawnPoints;


        private RoomInstance ownerRoom;
        private int aliveEnemies;

        /// <summary>
        /// Called by RoomInstance before spawning.
        /// </summary>
        public void Initialize(RoomInstance room)
        {
            ownerRoom = room;
            aliveEnemies = 0;
        }

        /// <summary>
        /// Spawns enemies; returns how many were actually spawned.
        /// </summary>
        public int SpawnEnemies()
        {
            if (DungeonMapManager.Instance == null)
            {
                Debug.LogWarning("[EnemySpawner] No DungeonMapManager; cannot spawn enemies.");
                return 0;
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("[EnemySpawner] No spawn points assigned.");
                return 0;
            }

            int count = Mathf.Max(1, spawnCount);
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                GameObject enemyPrefab = DungeonMapManager.Instance.GetRandomEnemyPrefab();
                if (enemyPrefab == null)
                {
                    Debug.LogWarning("[EnemySpawner] No enemy prefab available in current floor pool.");
                    break;
                }

                Transform point = spawnPoints[i % spawnPoints.Length];
                if (point == null) continue;

                GameObject enemy = Instantiate(enemyPrefab, point.position, point.rotation);
                var notifier = enemy.AddComponent<EnemyDeathNotifier>();
                notifier.OwnerSpawner = this;

                spawned++;
            }

            aliveEnemies += spawned;
            return spawned;
        }

        internal void NotifyEnemyDied()
        {
            aliveEnemies--;

            if (aliveEnemies < 0)
            {
                Debug.LogWarning($"[EnemySpawner] aliveEnemies went below zero on {name}. Clamping to 0.");
                aliveEnemies = 0;
            }

            if (ownerRoom != null)
            {
                ownerRoom.NotifyEnemyDied();
            }
        }
    }
}
