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
    public float perSecondAdd = 0.5f;

    [Tooltip("Maximum ramp time (seconds) to accumulate while holding.")]
    public float maxRampSeconds = 4.0f;

    [Header("Decay")]
    [Tooltip("How fast ramp time is lost per real second while NOT holding fire.")]
    public float decayRate = 1.0f;

    [Header("Extras")]
    public int extraPierceAtMax = 1;
    public float minFireInterval = 0.02f;
    static float baseFireRate = -1f;
    static int basePierce = -1;
    static float rampSeconds = 0f;
    static float lastShotTime = -1f;
    static bool isHeld = false;
    static float lastHoldChangeTime = -1f;

    public override void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats) => ResetState();
    public override void OnPrimaryUnequipped(GameObject owner) => ResetState();

    void ResetState()
    {
        baseFireRate = -1f;
        basePierce = -1;
        rampSeconds = 0f;
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

    public override void OnAboutToFire(WeaponStats activeStats)
    {
        if (activeStats == null) return;

        float now = Time.time;

        if (baseFireRate < 0f) baseFireRate = Mathf.Max(0.001f, activeStats.FireRate);
        if (basePierce < 0) basePierce = Mathf.Max(0, activeStats.PierceCount);

        float dt = (lastShotTime > 0f) ? (now - lastShotTime) : 0f;

        float buildTime = 0f;
        float idleTime = 0f;

        if (lastShotTime > 0f)
        {
            if (lastHoldChangeTime <= lastShotTime || lastHoldChangeTime < 0f)
            {
                if (isHeld) buildTime = dt; else idleTime = dt;
            }
            else
            {
                if (isHeld)
                {
                    idleTime = Mathf.Max(0f, lastHoldChangeTime - lastShotTime);
                    buildTime = Mathf.Max(0f, now - lastHoldChangeTime);
                }
                else
                {
                    idleTime = dt;
                }
            }
        }

        rampSeconds += buildTime;
        rampSeconds -= idleTime * Mathf.Max(0f, decayRate);

        maxRampSeconds = Mathf.Max(0f, maxRampSeconds);
        rampSeconds = Mathf.Clamp(rampSeconds, 0f, maxRampSeconds);

        float s = Mathf.Max(0.01f, startMultiplier);
        float a = Mathf.Max(0f, perSecondAdd);
        float mx = s + a * maxRampSeconds;
        float m = Mathf.Clamp(s + a * rampSeconds, s, mx);

        float targetInterval = Mathf.Max(minFireInterval, baseFireRate / Mathf.Max(0.01f, m));
        float fireDelta = activeStats.FireRate - targetInterval;
        activeStats.UpgradeFireRate(fireDelta);

        int targetPierce = (rampSeconds >= maxRampSeconds - 1e-3f)
            ? Mathf.Max(0, basePierce + extraPierceAtMax)
            : basePierce;

        int pierceDelta = targetPierce - activeStats.PierceCount;
        activeStats.UpgradePierce(pierceDelta);

        lastShotTime = now;
    }

    public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
    {
        if (upgrades == null) return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            switch (u.key)
            {
                case "Humming/StartMultiplier":
                    startMultiplier = Mathf.Max(0.01f, ApplyNumeric(startMultiplier, u));
                    break;

                case "Humming/PerSecondAdd":
                    perSecondAdd = Mathf.Max(0f, ApplyNumeric(perSecondAdd, u));
                    break;

                case "Humming/MaxRampSeconds":
                    maxRampSeconds = Mathf.Max(0f, ApplyNumeric(maxRampSeconds, u));
                    break;

                case "Humming/DecayRate":
                    decayRate = Mathf.Max(0f, ApplyNumeric(decayRate, u));
                    break;

                case "Humming/ExtraPierceAtMax":
                    extraPierceAtMax = Mathf.Max(0, Mathf.RoundToInt(ApplyNumeric(extraPierceAtMax, u)));
                    break;

                case "Humming/MinFireInterval":
                    minFireInterval = Mathf.Max(0.01f, ApplyNumeric(minFireInterval, u));
                    break;
            }
        }
    }
}
