using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Burn On Hit")]
public class BurnOnHitAbility : EssenceAbility
{
    public int ticks = 5;
    public float tickInterval = 0.3f;
    [Range(0f, 2f)] public float dpsFactor = 0.4f; // fraction of bullet damage per second

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats activeStats)
    {
        bullet.StartCoroutine(DoBurn(enemy, activeStats.damage));
    }

    IEnumerator DoBurn(Enemy enemy, float baseDamage)
    {
        if (enemy == null) yield break;
        float perTick = baseDamage * dpsFactor * tickInterval;
        for (int i = 0; i < ticks && enemy != null; i++)
        {
            enemy.TakeDamage(perTick, false);
            yield return new WaitForSeconds(tickInterval);
        }
    }
}
