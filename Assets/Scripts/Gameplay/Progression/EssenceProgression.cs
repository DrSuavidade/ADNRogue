using UnityEngine;
using System.Collections.Generic;
using Geneforge.Core.Stats;
using Geneforge.Gameplay.Abilities;

namespace Geneforge.Gameplay.Progression
{
    /// Tracks unlocked nodes per AnimalEssence for the current run.
    /// Attach this to a GameObject (e.g., Player or a small GameManager).
    public class EssenceProgression : MonoBehaviour
    {
        class Record
        {
            public HashSet<string> unlocked = new HashSet<string>();
        }

        private readonly Dictionary<AnimalEssence, Record> _map = new Dictionary<AnimalEssence, Record>();

        private Record Rec(AnimalEssence e)
        {
            if (e == null) return null;
            if (!_map.TryGetValue(e, out var r))
                _map[e] = r = new Record();
            return r;
        }

        public bool IsUnlocked(AnimalEssence e, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return false;
            var r = Rec(e);
            return r != null && r.unlocked.Contains(nodeId);
        }

        public IEnumerable<string> Unlocked(AnimalEssence e)
        {
            var r = Rec(e);
            return r != null ? r.unlocked : System.Array.Empty<string>();
        }

        public bool CanUnlock(AnimalEssence e, string nodeId)
        {
            if (e == null || e.skillTree == null || string.IsNullOrEmpty(nodeId)) return false;

            var node = e.skillTree.Get(nodeId);
            if (node == null) return false;

            var r = Rec(e);
            if (r.unlocked.Contains(nodeId)) return false;

            if (node.prerequisites != null)
            {
                for (int i = 0; i < node.prerequisites.Length; i++)
                {
                    string preId = node.prerequisites[i];
                    if (!r.unlocked.Contains(preId))
                        return false;
                }
            }

            return true;
        }

        public bool TryUnlock(AnimalEssence e, string nodeId, RunStats run)
        {
            if (e == null || e.skillTree == null || run == null || string.IsNullOrEmpty(nodeId)) return false;

            var node = e.skillTree.Get(nodeId);
            if (node == null || !CanUnlock(e, nodeId)) return false;

            if (!run.SpendDnaSplices(node.dnaCost))
                return false;

            Rec(e).unlocked.Add(nodeId);
            return true;
        }

        public List<StatModifier> GetActiveStatMods(AnimalEssence e)
        {
            var list = new List<StatModifier>();
            if (e == null || e.skillTree == null) return list;

            foreach (var id in Unlocked(e))
            {
                var node = e.skillTree.Get(id);
                if (node == null)
                {
                    Debug.LogWarning($"[EssenceProgression] Node '{id}' missing in '{e.name}' when aggregating stat mods.", this);
                    continue;
                }
                if (node?.statModifiers != null) list.AddRange(node.statModifiers);
            }
            return list;
        }

        public List<AbilityUpgrade> GetActiveAbilityUpgrades(AnimalEssence e)
        {
            var list = new List<AbilityUpgrade>();
            if (e == null || e.skillTree == null) return list;

            foreach (var id in Unlocked(e))
            {
                var node = e.skillTree.Get(id);
                if (node == null)
                {
                    Debug.LogWarning($"[EssenceProgression] Node '{id}' missing in '{e.name}' when aggregating ability upgrades.", this);
                    continue;
                }
                if (node?.abilityUpgrades != null) list.AddRange(node.abilityUpgrades);
            }
            return list;
        }

        public void ResetAll() => _map.Clear();
    }
}
