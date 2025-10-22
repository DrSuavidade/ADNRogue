using System;
using Geneforge.Gameplay.Abilities;

namespace Geneforge.Gameplay.Abilities
{
    [Serializable]
    public struct AbilityUpgrade
    {
        public string key;               // e.g., "Chain/MaxJumps", "Chain/Radius"
        public ModifierKind kind;        // Add or Multiply (same enum you already use)
        public float value;              // meaning depends on kind
    }
}
