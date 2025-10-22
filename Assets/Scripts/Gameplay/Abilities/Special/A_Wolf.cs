using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Wolf - Twin Fangs")]
public class A_WolfTwinFangs : EssenceAbility
{
    [Range(0f, 2f)] public float secondHitFactor = 0.7f;
    public float delayBetweenHits = 0.05f;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        if (!enemy || secondHitFactor <= 0f) return;
        bullet.StartCoroutine(SecondBite(enemy, stats.damage * secondHitFactor));
    }

    IEnumerator SecondBite(Enemy target, float dmg)
    {
        yield return new WaitForSeconds(delayBetweenHits);
        if (target) target.TakeDamage(dmg, false);
    }
}
