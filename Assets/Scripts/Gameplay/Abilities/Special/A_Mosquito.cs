using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

// <-- new usings
using Geneforge.Gameplay.Characters.Player;  // PlayerHealth lives here
using Geneforge.Core.Stats;                  // RunStats (currentHP, maxHP, etc.)

[CreateAssetMenu(menuName = "Geneforge/Abilities/Mosquito - Siphon")]
public class A_MosquitoSiphon : EssenceAbility
{
    [Header("Lifesteal")]
    [Range(0f, 1f)] public float lifestealPercent = 0.2f;
    public float maxHealPerHit = 25f;

    // cached references for speed
    static PlayerHealth cachedPlayerHealth;
    static RunStats     cachedRunStats;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (lifestealPercent <= 0f || stats == null) return;

        // Cache player refs if needed
        if (!cachedPlayerHealth)
            cachedPlayerHealth = Object.FindFirstObjectByType<PlayerHealth>()
                                  ?? Object.FindAnyObjectByType<PlayerHealth>();
        if (!cachedPlayerHealth) return;

        if (cachedRunStats == null)
            cachedRunStats = cachedPlayerHealth.GetComponent<RunStats>();
        if (cachedRunStats == null) return;

        // Heal = % of this bullet's damage, clamped
        float healAmt = Mathf.Min(stats.damage * lifestealPercent, maxHealPerHit);
        if (healAmt <= 0f) return;

        // Clamp to maxHP
        cachedRunStats.Heal(healAmt);

        // (Optional) If you have a UI event, you could raise it here to refresh the health bar
        // e.g., cachedRunStats.RaiseOnHealthChanged();
    }

    public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
    {
        if (upgrades == null) return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            switch (u.key)
            {
                case "Nova/LifestealPercent":
                    lifestealPercent = Mathf.Clamp01(ApplyNumeric(lifestealPercent, u));
                    break;

                case "Nova/MaxHealPerHit":
                    maxHealPerHit = Mathf.Max(0f, ApplyNumeric(maxHealPerHit, u));
                    break;
            }
        }
    }

}
