using System.Collections.Generic;
using UnityEngine;
using Geneforge.Core.Stats;
using Geneforge.Gameplay.Abilities;

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

        [Header("Stats Modifiers")]
        [Tooltip("List of stats to modify when picked up.")]
        [SerializeField] private List<RewardStatModifier> statModifiers = new List<RewardStatModifier>();

        [Header("Weapon Modifiers")]
        [Tooltip("Modify weapon stats (Damage, FireRate, etc).")]
        [SerializeField] private List<StatModifier> weaponModifiers = new List<StatModifier>();

        [Header("Custom Effects (Abilities)")]
        [Tooltip("Drag custom RewardEffect assets here (e.g. GainAbility, UnlockSkill).")]
        [SerializeField] private List<RewardEffect> customEffects = new List<RewardEffect>();

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
            Debug.Log($"[RewardItemData] Applying item: {itemName}");

            // 1. Apply Stats
            if (statModifiers.Count > 0)
            {
                var runStats = player.GetComponent<RunStats>();
                if (runStats == null) runStats = FindAnyObjectByType<RunStats>();

                if (runStats != null)
                {
                    foreach (var mod in statModifiers)
                    {
                        ApplyStat(runStats, mod);
                    }
                }
                else
                {
                    Debug.LogWarning("[RewardItemData] Could not find RunStats to apply modifiers.");
                }
            }

            // 2. Apply Weapon Modifiers
            if (weaponModifiers.Count > 0)
            {
                var gunSlots = player.GetComponent<Geneforge.Gameplay.Weapons.Slots.GunSlots>();
                if (gunSlots == null) gunSlots = player.GetComponentInChildren<Geneforge.Gameplay.Weapons.Slots.GunSlots>();

                if (gunSlots != null)
                {
                    foreach (var mod in weaponModifiers)
                    {
                        gunSlots.AddPassive(mod);
                    }
                    Debug.Log($"[RewardItemData] Added {weaponModifiers.Count} weapon modifiers.");
                }
                else
                {
                    Debug.LogWarning("[RewardItemData] Could not find GunSlots to apply weapon modifiers.");
                }
            }

            // 3. Apply Custom Effects
            foreach (var effect in customEffects)
            {
                if (effect != null)
                {
                    effect.Apply(player);
                }
            }

            // 4. Track in Inventory
            var inventory = player.GetComponent<RunInventory>();
            if (inventory == null)
            {
                // Auto-add inventory component if missing
                inventory = player.AddComponent<RunInventory>(); // This requires the type to be available, using namespace Geneforge.Gameplay.Items which we are in.
            }

            inventory.AddItem(this);
            Debug.Log($"[RewardItemData] Item '{itemName}' added to player inventory.");
        }

        private void ApplyStat(RunStats stats, RewardStatModifier mod)
        {
            int intVal = Mathf.RoundToInt(mod.value);
            switch (mod.stat)
            {
                case StatType.CurrentHealth:
                    if (mod.value > 0) stats.Heal(mod.value);
                    else stats.TakeDamage(-mod.value);
                    break;
                case StatType.MaxHealth:
                    stats.IncreaseMaxHP(mod.value);
                    break;
                case StatType.Lives:
                    stats.Lives += intVal;
                    break;
                case StatType.Currency:
                    if (intVal > 0) stats.AddCurrency(intVal);
                    else stats.SpendCurrency(-intVal);
                    break;
                case StatType.DnaSplices:
                    if (intVal > 0) stats.AddDnaSplices(intVal);
                    else stats.SpendDnaSplices(-intVal);
                    break;
                case StatType.Rolls:
                    stats.AddRolls(intVal);
                    break;
            }
        }
    }

    [System.Serializable]
    public struct RewardStatModifier
    {
        public StatType stat;
        [Tooltip("Positive to add/heal, negative to remove/damage.")]
        public float value;
    }

    public enum StatType
    {
        CurrentHealth,
        MaxHealth,
        Lives,
        Currency,
        DnaSplices,
        Rolls
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
