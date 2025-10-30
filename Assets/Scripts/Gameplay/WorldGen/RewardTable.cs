// Assets/Scripts/Gameplay/WorldGen/RewardTable.cs
using UnityEngine;
using System.Linq;

namespace Geneforge.Gameplay.WorldGen
{
    [CreateAssetMenu(menuName = "Geneforge/WorldGen/Reward Table")]
    public class RewardTable : ScriptableObject
    {
        [System.Serializable] public class Entry { public RewardKind kind; public float weight = 1f; }
        public Entry[] entries;

        public RewardKind Roll(System.Random rng)
        {
            if (entries == null || entries.Length == 0) return RewardKind.Essence;
            float sum = entries.Sum(e => Mathf.Max(0.0001f, e.weight));
            double r = rng.NextDouble() * sum;
            foreach (var e in entries)
            {
                r -= Mathf.Max(0.0001f, e.weight);
                if (r <= 0) return e.kind;
            }
            return entries[^1].kind;
        }
    }
}
