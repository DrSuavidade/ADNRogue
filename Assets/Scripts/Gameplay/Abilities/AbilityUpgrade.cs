using System;

namespace Geneforge.Gameplay.Abilities
{
    [Serializable]
    public struct AbilityUpgrade
    {
        public string key;
        public ModifierKind kind;
        public float value;
    }
}
