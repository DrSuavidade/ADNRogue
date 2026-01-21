using System.Collections.Generic;
using UnityEngine;

namespace Geneforge.Gameplay.Items
{
    /// <summary>
    /// ScriptableObject that defines a reward item.
    /// Each item can have custom logic executed when applied.
    /// The animation frames create a cycling "video-like" effect in the UI.
    /// </summary>
    [CreateAssetMenu(menuName = "Geneforge/Items/RewardItemData", fileName = "NewRewardItem")]
    public class RewardItemData : ScriptableObject
    {
        [Header("Display")]
        [Tooltip("Name shown in the UI.")]
        [SerializeField] private string itemName = "New Item";

        [Tooltip("Description of what this item does.")]
        [TextArea(2, 4)]
        [SerializeField] private string description = "";

        [Header("Animation Frames")]
        [Tooltip("Sprites that cycle to create an animated preview (like a video loop).")]
        [SerializeField] private List<Sprite> animationFrames = new List<Sprite>();

        [Tooltip("Frames per second for the animation cycle.")]
        [SerializeField] private float framesPerSecond = 8f;

        [Header("Rarity")]
        [SerializeField] private ItemRarity rarity = ItemRarity.Common;

        // ─────────────────────────────────────────────────────────────────
        // Public Accessors
        // ─────────────────────────────────────────────────────────────────

        public string ItemName => itemName;
        public string Description => description;
        public IReadOnlyList<Sprite> AnimationFrames => animationFrames;
        public float FramesPerSecond => framesPerSecond;
        public ItemRarity Rarity => rarity;

        /// <summary>
        /// Returns the first frame as a static icon fallback.
        /// </summary>
        public Sprite Icon => animationFrames != null && animationFrames.Count > 0 ? animationFrames[0] : null;

        // ─────────────────────────────────────────────────────────────────
        // Virtual Apply Method - Override in derived classes for custom logic
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called when the player selects this item. Override in subclasses to implement 
        /// specific item effects (stat boosts, abilities, etc).
        /// </summary>
        /// <param name="player">The player GameObject that receives the item.</param>
        public virtual void Apply(GameObject player)
        {
            Debug.Log($"[RewardItemData] Applied item: {itemName}");
        }
    }

    public enum ItemRarity
    {
        Common,
        Rare,
        Epic,
        Legendary,
        Mythic
    }
}
