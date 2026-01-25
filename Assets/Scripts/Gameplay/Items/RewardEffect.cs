using UnityEngine;

namespace Geneforge.Gameplay.Items
{
    /// <summary>
    /// Base class for custom effects applied by RewardItems.
    /// Create subclasses for specific abilities (e.g. "Grant Ability Component", "Unlock Skill Tree").
    /// </summary>
    public abstract class RewardEffect : ScriptableObject
    {
        [TextArea]
        [SerializeField] protected string developerNote;

        /// <summary>
        /// Apply the effect to the player.
        /// </summary>
        public abstract void Apply(GameObject player);

        /// <summary>
        /// Optional: Remove the effect (if items can be unequipped later).
        /// </summary>
        public virtual void Remove(GameObject player) { }
    }
}
