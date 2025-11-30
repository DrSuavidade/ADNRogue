using System;
using UnityEngine;
using System.Collections.Generic;

namespace Geneforge.Gameplay.Abilities
{
    [Serializable]
    public class EssenceSkillNode
    {
        public string id;
        public string title;
        [TextArea] public string description;
        public int dnaCost = 1;
        public string[] prerequisites;

        [Header("Effects")]
        public StatModifier[] statModifiers;
        public AbilityUpgrade[] abilityUpgrades;
    }

    [CreateAssetMenu(menuName = "Geneforge/Essence Skill Tree")]
    public class EssenceSkillTree : ScriptableObject
    {
        public EssenceSkillNode[] nodes;

        Dictionary<string, EssenceSkillNode> _byId;
        void OnEnable()
        {
            var safeNodes = nodes ?? Array.Empty<EssenceSkillNode>();
            _byId = new Dictionary<string, EssenceSkillNode>(safeNodes.Length);

            foreach (var n in safeNodes)
            {
                if (n == null || string.IsNullOrEmpty(n.id)) continue;

                if (_byId.ContainsKey(n.id))
                {
                    Debug.LogWarning($"[EssenceSkillTree] Duplicate node id '{n.id}' in tree '{name}'.", this); // NEW
                    continue;
                }

                _byId[n.id] = n;
            }
        }

        public EssenceSkillNode Get(string id)
            => (id != null && _byId != null && _byId.TryGetValue(id, out var n)) ? n : null;


        // --- Editor-time validation ---------------------------------------

        [ContextMenu("Validate Tree")]
        private void ValidateTree()
        {
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogWarning($"[EssenceSkillTree] '{name}' has no nodes.", this);
                return;
            }

            var idSet = new HashSet<string>();
            foreach (var n in nodes)
            {
                if (n == null)
                {
                    Debug.LogWarning($"[EssenceSkillTree] '{name}' has a null node entry.", this);
                    continue;
                }

                if (string.IsNullOrEmpty(n.id))
                {
                    Debug.LogWarning($"[EssenceSkillTree] Node with empty id in tree '{name}'.", this);
                    continue;
                }

                if (!idSet.Add(n.id))
                {
                    Debug.LogWarning($"[EssenceSkillTree] Duplicate id '{n.id}' in tree '{name}'.", this);
                }
            }

            foreach (var n in nodes)
            {
                if (n?.prerequisites == null) continue;

                foreach (var preId in n.prerequisites)
                {
                    if (string.IsNullOrEmpty(preId)) continue;
                    if (!idSet.Contains(preId))
                    {
                        Debug.LogWarning(
                            $"[EssenceSkillTree] Node '{n.id}' in '{name}' has unknown prerequisite '{preId}'.",
                            this);
                    }
                }
            }

            Debug.Log($"[EssenceSkillTree] Validation finished for '{name}'.", this);
        }
    }
}
