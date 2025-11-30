using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Crocodile - Cold-Blooded")]
public class A_CrocodileColdBlooded : EssenceAbility
{
    [Header("Bonus Crit (per hit, additional to normal crit)")]
    [Range(0f, 1f)] public float bonusCritChance = 0.20f;
    [Min(1f)] public float bonusCritMultiplier = 1.5f;

    [Header("Execute")]
    [Range(0f, 1f)] public float executeThreshold = 0.15f;
    [Min(1f)] public float executeDamageFactor = 10f;

    public override void OnBulletSpawn(Bullet bullet, WeaponStats activeStats) { }

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!enemy || !bullet) return;

        if (bonusCritChance > 0f && Random.value < bonusCritChance)
        {
            float extra = Mathf.Max(0f, bullet.damage) * (Mathf.Max(1f, bonusCritMultiplier) - 1f);
            if (extra > 0f) enemy.TakeDamage(extra, true);
        }

        if (TryReadHealth(enemy, out float hp, out float max) && max > 0f)
        {
            float frac = hp / max;
            if (frac <= executeThreshold)
            {
                float bonus = Mathf.Max(0f, stats.Damage) * Mathf.Max(1f, executeDamageFactor);
                enemy.TakeDamage(bonus, false);
            }
        }
    }

    bool TryReadHealth(Enemy e, out float hp, out float max)
    {
        hp = max = -1f;
        var t = e.GetType();

        var fHP = t.GetField("currentHealth") ?? t.GetField("health") ?? t.GetField("hp");
        var fMax = t.GetField("maxHealth") ?? t.GetField("healthMax") ?? t.GetField("maxHP");
        if (fHP != null && fMax != null) { hp = (float)fHP.GetValue(e); max = (float)fMax.GetValue(e); return true; }

        var pHP = t.GetProperty("CurrentHealth") ?? t.GetProperty("Health");
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
