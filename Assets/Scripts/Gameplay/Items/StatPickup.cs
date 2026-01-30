using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Geneforge.Core.Stats;
using Geneforge.Gameplay.Map;

namespace Geneforge.Gameplay.Items
{
    /// <summary>
    /// A pickup that immediately modifies RunStats when collected.
    /// Used for single-purpose items like Health Potions, Coin Piles, DNA Fragments, etc.
    /// </summary>
    public class StatPickup : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("The stats to modify when this object is picked up.")]
        [SerializeField] private List<RewardStatModifier> modifiers = new List<RewardStatModifier>();

        [Header("Events")]
        [Tooltip("Event invoked when the pickup is collected (play sound, spawn particles, etc).")]
        [SerializeField] private UnityEvent onPickup;

        [Header("Settings")]
        [SerializeField] private bool destroyOnPickup = true;
        [SerializeField] private string pickupTag = "Player";

        private bool _isCollected = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;

            if (other.CompareTag(pickupTag))
            {
                Collect(other.gameObject);
            }
        }

        private void Collect(GameObject player)
        {
            var runStats = player.GetComponent<RunStats>();
            // Fallback if RunStats is not on the player (e.g. global manager)
            if (runStats == null) runStats = FindAnyObjectByType<RunStats>();

            if (runStats != null)
            {
                _isCollected = true;

                // Apply all modifiers
                float multiplier = 1f;
                if (DungeonMapManager.Instance != null)
                {
                    multiplier = DungeonMapManager.Instance.CurrentStatMultiplier;
                }

                foreach (var mod in modifiers)
                {
                    ApplyStat(runStats, mod, multiplier);
                }

                // Feedback
                onPickup?.Invoke();

                Debug.Log($"[StatPickup] Collected by {player.name}. Applied {modifiers.Count} modifiers with x{multiplier:F2} timeline multiplier.");

                if (destroyOnPickup)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                Debug.LogWarning($"[StatPickup] Could not find RunStats component for {player.name}. Pickup ignored.");
            }
        }

        /// <summary>
        /// Applies a single stat modifier to the RunStats.
        /// Logic mirrors RewardItemData.ApplyStat.
        /// </summary>
        private void ApplyStat(RunStats stats, RewardStatModifier mod, float multiplier)
        {
            // Only apply multiplier to additive values (absolute amounts like +10 gold, +25 hp)
            // Percentage multipliers (like x1.2 damage) should usually remain constant across timelines.
            float finalValue = mod.value;
            if (mod.kind == ModifierKind.Add)
            {
                finalValue *= multiplier;
            }

            int intVal = Mathf.RoundToInt(finalValue);

            switch (mod.stat)
            {
                case StatType.CurrentHealth:
                    if (mod.kind == ModifierKind.Add)
                    {
                        if (finalValue > 0) stats.Heal(finalValue);
                        else stats.TakeDamage(-finalValue);
                    }
                    else // Multiply
                    {
                        float currentHP = stats.CurrentHP;
                        float newHP = currentHP * finalValue;
                        float delta = newHP - currentHP;
                        if (delta > 0) stats.Heal(delta);
                        else stats.TakeDamage(-delta);
                    }
                    break;

                case StatType.MaxHealth:
                    if (mod.kind == ModifierKind.Add)
                    {
                        // Note: IncreaseMaxHP automatically increases CurrentHP by the same amount inside RunStats.cs
                        stats.IncreaseMaxHP(finalValue);
                    }
                    else // Multiply
                    {
                        float delta = stats.MaxHP * (finalValue - 1f);
                        stats.IncreaseMaxHP(delta);
                    }
                    break;

                case StatType.Lives:
                    if (mod.kind == ModifierKind.Add)
                    {
                        stats.Lives += intVal;
                    }
                    else // Multiply
                    {
                        stats.Lives = Mathf.RoundToInt(stats.Lives * finalValue);
                    }
                    break;

                case StatType.Currency:
                    if (mod.kind == ModifierKind.Add)
                    {
                        if (intVal > 0) stats.AddCurrency(intVal);
                        else stats.SpendCurrency(-intVal);
                    }
                    else // Multiply
                    {
                        int newAmount = Mathf.RoundToInt(stats.Currency * finalValue);
                        int delta = newAmount - stats.Currency;
                        if (delta > 0) stats.AddCurrency(delta);
                        else stats.SpendCurrency(-delta);
                    }
                    break;

                case StatType.DnaSplices:
                    if (mod.kind == ModifierKind.Add)
                    {
                        if (intVal > 0) stats.AddDnaSplices(intVal);
                        else stats.SpendDnaSplices(-intVal);
                    }
                    else // Multiply
                    {
                        int newAmount = Mathf.RoundToInt(stats.DnaSplices * finalValue);
                        int delta = newAmount - stats.DnaSplices;
                        if (delta > 0) stats.AddDnaSplices(delta);
                        else stats.SpendDnaSplices(-delta);
                    }
                    break;

                case StatType.Rolls:
                    if (mod.kind == ModifierKind.Add)
                    {
                        stats.AddRolls(intVal);
                    }
                    else // Multiply
                    {
                        int newAmount = Mathf.RoundToInt(stats.Rolls * finalValue);
                        int delta = newAmount - stats.Rolls;
                        stats.AddRolls(delta);
                    }
                    break;
            }
        }
    }
}
