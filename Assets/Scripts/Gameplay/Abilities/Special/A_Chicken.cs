using UnityEngine;
using System.Collections.Generic;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Chicken - Beak Cone")]
public class ChickenBeakConeAbility : EssenceAbility
{
    [Header("Cone")]
    [Range(1f, 180f)] public float coneAngle = 35f;
    public float coneRange = 6f;
    [Header("Damage/FX")]
    public float damageFactor = 1.0f;     // x bullet damage
    [Range(0f, 1f)] public float hitFalloff = 0f; // extra falloff across distance (0 = none)
    public float knockback = 0f;

    public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
    {
        Vector3 origin = bullet.transform.position;
        Vector3 fwd = bullet.transform.forward;

        var hits = Physics.OverlapSphere(origin, coneRange, ~0, QueryTriggerInteraction.Ignore);
        var done = new HashSet<Enemy>();

        for (int i = 0; i < hits.Length; i++)
        {
            var e = hits[i].GetComponent<Enemy>();
            if (e == null || done.Contains(e)) continue;

            Vector3 to = e.transform.position - origin;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.001f) continue;

            float ang = Vector3.Angle(fwd, to);
            if (ang <= coneAngle * 0.5f)
            {
                float dmg = stats.damage * damageFactor;
                if (hitFalloff > 0f)
                {
                    float t = Mathf.Clamp01(dist / coneRange);
                    dmg *= Mathf.Lerp(1f, 1f - hitFalloff, t);
                }

                e.TakeDamage(dmg, false);

                if (knockback > 0f)
                {
                    var dir = to.normalized; dir.y = 0f;
                    e.ApplyKnockback(dir, knockback);
                }

                done.Add(e);
            }
        }

        // Replace the bullet with the cone hit — remove the projectile.
        Destroy(bullet.gameObject);
    }
}
