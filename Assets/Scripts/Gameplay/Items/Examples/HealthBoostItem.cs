using UnityEngine;
using Geneforge.Gameplay.Progression;

namespace Geneforge.Gameplay.Items.Examples
{
    /// <summary>
    /// Example item that heals the player.
    /// Note: MaxHP is currently read-only in RunStats. This example heals the player.
    /// To actually increase MaxHP, you would need to modify RunStats to support that.
    /// </summary>
    [CreateAssetMenu(menuName = "Geneforge/Items/Examples/HealthBoostItem", fileName = "HealthBoostItem")]
    public class HealthBoostItem : RewardItemData
    {
        [Header("Health Boost Settings")]
        [Tooltip("Amount to heal the player.")]
        [SerializeField] private float healAmount = 25f;

        public override void Apply(GameObject player)
        {
            base.Apply(player);

            // Use the RunSession system to heal the player
            var runSession = RunSession.Instance;
            if (runSession != null && runSession.Run != null)
            {
                runSession.Run.Heal(healAmount);
                Debug.Log($"[HealthBoostItem] Healed player by {healAmount}. Current HP: {runSession.Run.CurrentHP}/{runSession.Run.MaxHP}");
            }
            else
            {
                Debug.LogWarning("[HealthBoostItem] RunSession not found.");
            }
        }
    }
}
