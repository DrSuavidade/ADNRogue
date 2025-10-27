using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Stats;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Hummingbird - Momentum Fire")]
public class HummingbirdMomentumFireAbility : EssenceAbility
{
    [Header("Ramp multipliers")]
    [Tooltip("Starting fire-rate multiplier at 0s (0.5 = half base fire rate).")]
    public float startMultiplier = 0.5f;

    [Tooltip("How much fire-rate multiplier increases per second while holding fire.")]
    public float perSecondAdd    = 0.5f;

    [Tooltip("Maximum ramp time (seconds) to accumulate while holding.")]
    public float maxRampSeconds  = 4.0f;

    [Header("Decay")]
    [Tooltip("How fast ramp time is lost per real second while NOT holding fire.")]
    public float decayRate = 1.0f; // 1s of release removes 1s of ramp

    [Header("Extras")]
    public int   extraPierceAtMax = 1;
    public float minFireInterval  = 0.02f;

    // runtime state (shared for the equipped run)
    static float baseFireRate = -1f;   // seconds/shot
    static int   basePierce   = -1;
    static float rampSeconds  = 0f;    // 0..maxRampSeconds
    static float lastShotTime = -1f;

    // fire-hold tracking
    static bool  isHeld = false;
    static float lastHoldChangeTime = -1f;

    public override void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats) => ResetState();
    public override void OnPrimaryUnequipped(GameObject owner)                         => ResetState();

    void ResetState()
    {
        baseFireRate = -1f;
        basePierce   = -1;
        rampSeconds  = 0f;
        lastShotTime = -1f;
        isHeld = false;
        lastHoldChangeTime = -1f;
    }

    public override void OnFireHeldStart()
    {
        isHeld = true;
        lastHoldChangeTime = Time.time;
    }

    public override void OnFireHeldStop()
    {
        isHeld = false;
        lastHoldChangeTime = Time.time;
    }

    // Runs immediately before PlayerController schedules the next shot
    public override void OnAboutToFire(WeaponStats activeStats)
    {
        float now = Time.time;

        if (baseFireRate < 0f) baseFireRate = Mathf.Max(0.001f, activeStats.fireRate);
        if (basePierce   < 0)  basePierce   = Mathf.Max(0, activeStats.pierceCount);

        // Time since last shot
        float dt = (lastShotTime > 0f) ? (now - lastShotTime) : 0f;

        // Compute how much of that dt we were holding vs released
        float buildTime = 0f; // contributes +dt to ramp
        float idleTime  = 0f; // contributes -dt*decayRate to ramp

        if (lastShotTime > 0f)
        {
            if (lastHoldChangeTime <= lastShotTime || lastHoldChangeTime < 0f)
            {
                // No hold-state change since the last shot
                if (isHeld) buildTime = dt; else idleTime = dt;
            }
            else
            {
                // Hold-state changed after the last shot
                if (isHeld)
                {
                    // We are currently holding (this shot fired). At some time after the last shot,
                    // holding began. The part before that was idle.
                    idleTime  = Mathf.Max(0f, lastHoldChangeTime - lastShotTime);
                    buildTime = Mathf.Max(0f, now - lastHoldChangeTime);
                }
                else
                {
                    // Shouldn't happen because we can't fire if not held, but keep it safe:
                    idleTime = dt;
                }
            }
        }

        // Update rampSeconds: grows with buildTime (while held), decays only with time we were released
        rampSeconds += buildTime;
        rampSeconds -= idleTime * Mathf.Max(0f, decayRate);

        // Clamp 0..max
        maxRampSeconds = Mathf.Max(0f, maxRampSeconds);
        rampSeconds = Mathf.Clamp(rampSeconds, 0f, maxRampSeconds);

        // Compute multiplier and apply (interval / multiplier)
        float s  = Mathf.Max(0.01f, startMultiplier);
        float a  = Mathf.Max(0f,    perSecondAdd);
        float mx = s + a * maxRampSeconds;
        float m  = Mathf.Clamp(s + a * rampSeconds, s, mx);

        activeStats.fireRate = Mathf.Max(minFireInterval, baseFireRate / Mathf.Max(0.01f, m));

        // Extra pierce only at max ramp
        activeStats.pierceCount = (rampSeconds >= maxRampSeconds - 1e-3f)
            ? Mathf.Max(0, basePierce + extraPierceAtMax)
            : basePierce;

        lastShotTime = now;
    }
}
