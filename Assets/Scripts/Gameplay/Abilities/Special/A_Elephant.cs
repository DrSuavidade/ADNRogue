using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Elephant - Stampede Shot")]
public class A_ElephantStampede : EssenceAbility
{
    [Header("Projectile")]
    public float sizeMult = 1.8f;

    [Header("Impact AoE")]
    public float aoeRadius = 4.5f;
    public float aoeDamageFactor = 0.8f;   // x bullet damage to others
    public float knockback = 10f;

    public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
    {
        bullet.transform.localScale *= sizeMult;
    }

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        Vector3 center = enemy ? enemy.transform.position : bullet.transform.position;
        var cols = Physics.OverlapSphere(center, aoeRadius, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < cols.Length; i++)
        {
            var e = cols[i].GetComponent<Enemy>();
            if (!e || e == enemy) continue;

            e.TakeDamage(stats.damage * aoeDamageFactor, false);

            if (knockback > 0f)
            {
                Vector3 dir = (e.transform.position - center); dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f) e.ApplyKnockback(dir.normalized, knockback);
            }
        }
    }
}
