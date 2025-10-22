using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Crocodile - Cold-Blooded")]
public class A_CrocodileColdBlooded : EssenceAbility
{
    [Header("Crit boost")]
    [Range(0f, 1f)] public float critChanceAdd = 0.15f;
    public float critMultAdd = 0.5f;

    [Header("Execute")]
    [Range(0f, 1f)] public float executePercent = 0.15f;   // execute below 15% HP
    public float executeDamageFactor = 10f;                 // big bonus to finish off

    public override void OnBulletSpawn(Bullet bullet, WeaponStats activeStats)
    {
        activeStats.critChance    = Mathf.Clamp01(activeStats.critChance + critChanceAdd);
        activeStats.critMultiplier = Mathf.Max(1f, activeStats.critMultiplier + critMultAdd);
    }

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (enemy == null) return;

        // Try to read health fraction via common field/property names (no Enemy changes needed)
        float hp = -1f, max = -1f;
        if (!TryGetHealth(enemy, out hp, out max) || max <= 0f) return;

        float frac = hp / max;
        if (frac <= executePercent)
        {
            enemy.TakeDamage(stats.damage * executeDamageFactor, false);
        }
    }

    bool TryGetHealth(Enemy e, out float hp, out float max)
    {
        hp = max = -1f;
        var t = e.GetType();

        // fields
        var fHP  = t.GetField("currentHealth") ?? t.GetField("health") ?? t.GetField("hp");
        var fMax = t.GetField("maxHealth") ?? t.GetField("healthMax") ?? t.GetField("maxHP");
        if (fHP != null && fMax != null) { hp = (float)fHP.GetValue(e); max = (float)fMax.GetValue(e); return true; }

        // properties
        var pHP  = t.GetProperty("CurrentHealth") ?? t.GetProperty("Health");
        var pMax = t.GetProperty("MaxHealth");
        if (pHP != null && pMax != null) { hp = (float)pHP.GetValue(e); max = (float)pMax.GetValue(e); return true; }

        return false;
    }
}
