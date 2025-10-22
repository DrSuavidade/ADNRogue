using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace Geneforge.Gameplay.Abilities
{
    [Serializable]
    public class EssenceSkillNode
    {
        public string id;                      // unique within tree
        public string title;
        [TextArea] public string description;
        public int dnaCost = 1;
        public string[] prerequisites;         // ids which must be unlocked first

        [Header("Effects")]
        public StatModifier[] statModifiers;   // uses your existing StatModifier
        public AbilityUpgrade[] abilityUpgrades;
    }

    [CreateAssetMenu(menuName = "Geneforge/Essence Skill Tree")]
    public class EssenceSkillTree : ScriptableObject
    {
        public EssenceSkillNode[] nodes;

        Dictionary<string, EssenceSkillNode> _byId;
        void OnEnable()
        {
            _byId = (nodes ?? Array.Empty<EssenceSkillNode>()).ToDictionary(n => n.id);
        }

        public EssenceSkillNode Get(string id)
            => (id != null && _byId != null && _byId.TryGetValue(id, out var n)) ? n : null;
    }
}
