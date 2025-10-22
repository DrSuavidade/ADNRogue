using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Knockback Shockwave")]
public class KnockbackShockwaveAbility : EssenceAbility
{
    public float radius = 4f;
    public float force = 8f;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats activeStats)
    {
        var hits = Physics.OverlapSphere(enemy.transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var e = hits[i].GetComponent<Enemy>();
            if (e == null || e == enemy) continue;
            Vector3 dir = (e.transform.position - enemy.transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) e.ApplyKnockback(dir.normalized, force);
        }
    }
}
