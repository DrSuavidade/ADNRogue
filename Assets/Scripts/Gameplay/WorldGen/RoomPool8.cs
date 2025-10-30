// Assets/Scripts/Gameplay/WorldGen/RoomPool8.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Geneforge.Gameplay.WorldGen
{
    [CreateAssetMenu(menuName = "Geneforge/WorldGen/Room Pool (8-way)")]
    public class RoomPool8 : ScriptableObject
    {
        [System.Serializable] public class Entry { public RoomTemplate8 template; public float weight = 1f; }
        public RoomKind kind;
        public List<Entry> entries = new();

        public RoomTemplate8 Pick(System.Random rng, HashSet<Dir8> requiredDoors)
        {
            var cands = entries.Where(e =>
                e.template && e.template.kind == kind &&
                requiredDoors.All(d => e.template.Supports(d))
            ).ToList();

            if (cands.Count == 0) return null;

            float sum = cands.Sum(c => Mathf.Max(0.0001f, c.weight));
            double r = rng.NextDouble() * sum;
            foreach (var c in cands)
            {
                r -= Mathf.Max(0.0001f, c.weight);
                if (r <= 0) return c.template;
            }
            return cands[^1].template;
        }
    }
}
