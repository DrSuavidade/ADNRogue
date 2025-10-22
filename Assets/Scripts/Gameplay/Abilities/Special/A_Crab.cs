using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Crab - Bubble Burst")]
public class A_CrabBubbleBurst : EssenceAbility
{
    [Header("Weapon multipliers (applied to active stats)")]
    [Range(0.05f, 1f)] public float accuracyMult = 0.35f;   // worse accuracy
    [Range(0.05f, 1f)] public float damageMult   = 0.6f;    // lower damage
    [Range(1f, 10f)]  public float fireRateMult  = 3.0f;    // much faster (lower interval)

    [Header("Bubble feel")]
    public float bubbleDrag = 1.2f;                          // slows in air
    public float sizeMult   = 0.9f;

    public override void OnBulletSpawn(Bullet bullet, WeaponStats activeStats)
    {
        // Mutate the shared active stats so subsequent shots are spammy
        activeStats.accuracy = Mathf.Clamp01(activeStats.accuracy * accuracyMult);
        activeStats.damage   = Mathf.Max(0f, activeStats.damage   * damageMult);
        activeStats.fireRate = Mathf.Max(0.02f, activeStats.fireRate / fireRateMult);

        // Bubble feel on this projectile
        var rb = bullet.GetComponent<Rigidbody>();
        if (rb != null) rb.linearDamping = bubbleDrag;

        bullet.transform.localScale *= sizeMult;
    }
}
