using UnityEngine;

namespace Geneforge.Gameplay.Map
{
    public class RewardSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Prefab for the key pickup; must have a KeyPickup component.")]
        public GameObject keyPickupPrefab;

        [Tooltip("Optional: some generic reward prefab if you want non-key loot too.")]
        public GameObject regularRewardPrefab;

        [Header("State (runtime)")]
        [SerializeField] private bool spawnKey;

        public bool WillSpawnKey => spawnKey;

        public void ConfigureKeySpawn(bool shouldSpawnKey)
        {
            spawnKey = shouldSpawnKey;
        }

        /// <summary>
        /// Call this from your combat-clear logic.
        /// </summary>
        public void SpawnRewards()
        {
            if (spawnKey && keyPickupPrefab != null)
            {
                Instantiate(keyPickupPrefab, transform.position, transform.rotation);
            }
            else if (regularRewardPrefab != null)
            {
                Instantiate(regularRewardPrefab, transform.position, transform.rotation);
            }
        }
    }
}
