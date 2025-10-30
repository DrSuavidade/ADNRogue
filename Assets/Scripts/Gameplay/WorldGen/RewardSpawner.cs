// Assets/Scripts/Gameplay/WorldGen/RewardSpawner.cs
using UnityEngine;

namespace Geneforge.Gameplay.WorldGen
{
    public class RewardSpawner : MonoBehaviour
    {
        public GameObject essenceChestPrefab;
        public GameObject upgradePickupPrefab;
        public GameObject healPickupPrefab;
        public GameObject goldPickupPrefab;
        public GameObject keyShardPrefab;

        public void Spawn(RewardKind kind, Transform parent, Vector3 localOffset)
        {
            GameObject prefab = kind switch
            {
                RewardKind.Essence  => essenceChestPrefab,
                RewardKind.Upgrade  => upgradePickupPrefab,
                RewardKind.Heal     => healPickupPrefab,
                RewardKind.Gold     => goldPickupPrefab,
                RewardKind.KeyShard => keyShardPrefab,
                _ => essenceChestPrefab
            };
            if (!prefab) return;

            var go = Instantiate(prefab, parent);
            go.transform.localPosition = localOffset;
        }
    }
}
