using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    public class RewardSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool representsBossReward;

        [Header("State (runtime)")]
        [SerializeField] private bool spawnKey;
        [SerializeField] private bool hasSpawned;

        public bool WillSpawnKey => spawnKey;

        /// <summary>
        /// Called by RoomInstance when it is chosen as the key room.
        /// </summary>
        public void ConfigureKeySpawn(bool shouldSpawnKey)
        {
            spawnKey = shouldSpawnKey;
        }

        /// <summary>
        /// Called by RoomInstance when the room is cleared.
        /// </summary>
        public void SpawnRewards()
        {
            if (hasSpawned) return;
            hasSpawned = true;

            if (DungeonMapManager.Instance == null)
            {
                Debug.LogWarning("[RewardSpawner] No DungeonMapManager instance found.");
                return;
            }

            if (spawnKey)
            {
                DungeonMapManager.Instance.SpawnKeyAt(this);
            }
            else if (representsBossReward)
            {
                DungeonMapManager.Instance.SpawnBossRewardAt(this);
            }
            else
            {
                DungeonMapManager.Instance.SpawnRewardAt(this);
            }
        }
    }
}
