using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Hummingbird - Momentum Fire")]
public class HummingbirdMomentumFireAbility : EssenceAbility
{
    [Header("Ramp")]
    public float rampPerSecond = 1.2f;     // time to reach full momentum ≈ 0.8s
    public float decayPerSecond = 2.0f;    // ramp falls when pausing
    public float decayDelay = 0.25f;       // grace window between shots

    [Header("Effect")]
    public float maxFireRateMultiplier = 1.8f; // >1 means faster firing (lower interval)
    public int extraPierceAtMax = 1;

    // Shared per-run state (one primary hummingbird expected)
    static float baseFireRate = -1f;
    static int   basePierce = -1;
    static float ramp = 0f;                // 0..1
    static float lastShotTime = -999f;

    public override void OnBulletSpawn(Bullet bullet, WeaponStats activeStats)
    {
        float now = Time.time;

        // Capture baselines once (first shot after equip)
        if (baseFireRate < 0f) baseFireRate = activeStats.fireRate;
        if (basePierce   < 0)  basePierce   = activeStats.pierceCount;

        // Update ramp based on time gap since last shot
        float dt = (lastShotTime > 0f) ? (now - lastShotTime) : 0f;
        if (dt <= decayDelay)
            ramp += rampPerSecond * Mathf.Max(0f, dt);
        else
            ramp -= decayPerSecond * (dt - decayDelay);

        ramp = Mathf.Clamp01(ramp);

        // Apply to the shared stats object (affects subsequent shots)
        float mult = Mathf.Lerp(1f, maxFireRateMultiplier, ramp);     // 1..max
        activeStats.fireRate = Mathf.Max(0.02f, baseFireRate / mult); // smaller interval at higher mult

        if (Mathf.Approximately(ramp, 1f))
            activeStats.pierceCount = Mathf.Max(0, basePierce + extraPierceAtMax);
        else
            activeStats.pierceCount = basePierce;

        lastShotTime = now;
    }
}
