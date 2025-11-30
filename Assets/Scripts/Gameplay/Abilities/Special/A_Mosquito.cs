using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Core.Stats;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Mosquito - Siphon")]
public class A_MosquitoSiphon : EssenceAbility
{
    [Header("Lifesteal")]
    [Range(0f, 1f)] public float lifestealPercent = 0.2f;
    public float maxHealPerHit = 25f;
    static PlayerHealth cachedPlayerHealth;
    static RunStats cachedRunStats;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (lifestealPercent <= 0f || stats == null) return;

        if (!cachedPlayerHealth)
            cachedPlayerHealth = FindFirstObjectByType<PlayerHealth>()
                                  ?? FindAnyObjectByType<PlayerHealth>();
        if (!cachedPlayerHealth) return;

        if (cachedRunStats == null)
            cachedRunStats = cachedPlayerHealth.GetComponent<RunStats>();
        if (cachedRunStats == null) return;

        float healAmt = Mathf.Min(stats.Damage * lifestealPercent, maxHealPerHit);
        if (healAmt <= 0f) return;

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
