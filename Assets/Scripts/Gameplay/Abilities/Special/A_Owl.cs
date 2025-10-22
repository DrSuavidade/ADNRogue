using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Owl - Hunter's Mark")]
public class OwlHuntersMarkAbility : EssenceAbility
{
    [Header("Marking")]
    public float markDuration = 8f;          // 0 = no expiry
    [Header("Pop")]
    public float popDamageFactor = 1.5f;     // x bullet damage
    public float splashRadius = 0f;          // optional AoE on pop (0 = none)
    public float splashDamageFactor = 0.5f;  // x pop damage to others

    static Enemy marked;
    static float markExpireAt;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        float now = Time.time;

        // Pop if hitting the marked target and still valid
        if (marked != null && enemy == marked && (markDuration <= 0f || now <= markExpireAt))
        {
            float popDmg = stats.damage * popDamageFactor;
            enemy.TakeDamage(popDmg, false);

            if (splashRadius > 0f)
            {
                var cols = Physics.OverlapSphere(enemy.transform.position, splashRadius, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < cols.Length; i++)
                {
                    var e2 = cols[i].GetComponent<Enemy>();
                    if (e2 != null && e2 != enemy) e2.TakeDamage(popDmg * splashDamageFactor, false);
                }
            }

            marked = null; markExpireAt = 0f;
            return;
        }

        // Otherwise (re)apply mark to this enemy
        marked = enemy;
        markExpireAt = (markDuration > 0f) ? now + markDuration : float.PositiveInfinity;
    }
}
