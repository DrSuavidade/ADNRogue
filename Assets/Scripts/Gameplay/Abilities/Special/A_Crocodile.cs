using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Crocodile - Cold-Blooded")]
public class A_CrocodileColdBlooded : EssenceAbility
{
    [Header("Bonus Crit (per hit, additional to normal crit)")]
    [Range(0f, 1f)] public float bonusCritChance = 0.20f;  // extra roll
    [Min(1f)] public float bonusCritMultiplier = 1.5f;     // extra damage factor when bonus crit procs

    [Header("Execute")]
    [Range(0f, 1f)] public float executeThreshold = 0.15f; // execute below 15% HP
    [Min(1f)] public float executeDamageFactor = 10f;      // big nuke when under threshold

    // IMPORTANT: do NOT mutate shared stats here
    public override void OnBulletSpawn(Bullet bullet, WeaponStats activeStats) { }

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!enemy || !bullet) return;

        // --- Bonus crit (doesn't change global stats; just adds extra damage on top) ---
        if (bonusCritChance > 0f && Random.value < bonusCritChance)
        {
            // add only the extra part of the crit (e.g., +50% if multiplier=1.5)
            float extra = Mathf.Max(0f, bullet.damage) * (Mathf.Max(1f, bonusCritMultiplier) - 1f);
            if (extra > 0f) enemy.TakeDamage(extra, true);
        }

        // --- Execute check (read enemy HP via common members; no Enemy changes required) ---
        if (TryReadHealth(enemy, out float hp, out float max) && max > 0f)
        {
            float frac = hp / max;
            if (frac <= executeThreshold)
            {
                // Big finishing chunk; still uses normal damage pipeline
                float bonus = Mathf.Max(0f, stats.damage) * Mathf.Max(1f, executeDamageFactor);
                enemy.TakeDamage(bonus, false);
            }
        }
    }

    bool TryReadHealth(Enemy e, out float hp, out float max)
    {
        hp = max = -1f;
        var t = e.GetType();

        // fields
        var fHP  = t.GetField("currentHealth") ?? t.GetField("health") ?? t.GetField("hp");
        var fMax = t.GetField("maxHealth")     ?? t.GetField("healthMax") ?? t.GetField("maxHP");
        if (fHP != null && fMax != null) { hp = (float)fHP.GetValue(e); max = (float)fMax.GetValue(e); return true; }

        // properties
        var pHP  = t.GetProperty("CurrentHealth") ?? t.GetProperty("Health");
        var pMax = t.GetProperty("MaxHealth");
        if (pHP != null && pMax != null) { hp = (float)pHP.GetValue(e); max = (float)pMax.GetValue(e); return true; }

        return false;
    }
    public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
    {
        if (upgrades == null) return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            switch (u.key)
            {
                case "Croc/BonusCritChance":
                    bonusCritChance = Mathf.Clamp01(ApplyNumeric(bonusCritChance, u));
                    break;

                case "Croc/BonusCritMultiplier":
                    bonusCritMultiplier = Mathf.Max(1f, ApplyNumeric(bonusCritMultiplier, u));
                    break;

                case "Croc/ExecuteThreshold":
                    executeThreshold = Mathf.Clamp01(ApplyNumeric(executeThreshold, u));
                    break;

                case "Croc/ExecuteDamageFactor":
                    executeDamageFactor = Mathf.Max(1f, ApplyNumeric(executeDamageFactor, u));
                    break;
            }
        }
    }

}
