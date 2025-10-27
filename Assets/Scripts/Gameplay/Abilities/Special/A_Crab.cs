using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Crab - Bubble Burst")]
public class A_CrabBubbleBurst : EssenceAbility
{
    [Header("Forced Weapon Accuracy")]
    [Range(0f, 1f)]  public  float forcedAccuracy = 0.10f;     // super inaccurate
    [Range(0f, 90f)] public  float forcedInaccuracyHalf = 25f; // ±25° yaw jitter

    [Header("Weapon feel (applied pre-fire)")]
    [Range(0.05f, 1f)] public float damageMult   = 0.60f;      // lower damage
    [Range(0.1f, 20f)] public float fireRateMult = 4.0f;       // interval = baseline / mult

    [Header("Bubble feel (per projectile)")]
    public float bubbleDrag  = 1.2f;  // slows in air
    public float sizeMult    = 0.9f;
    public bool  sphereVisual = true;

    // --- stat baselines via previous multipliers (so live changes work both directions) ---
    static float _prevFireRateMult = 1f;
    static float _prevDamageMult   = 1f;

    public override void OnPrimaryEquipped(GameObject owner, WeaponStats activeStats)
    {
        _prevFireRateMult = 1f;
        _prevDamageMult   = 1f;
    }

    public override void OnPrimaryUnequipped(GameObject owner)
    {
        _prevFireRateMult = 1f;
        _prevDamageMult   = 1f;
    }

    // Run RIGHT BEFORE PlayerController schedules the next shot
    public override void OnAboutToFire(WeaponStats activeStats)
    {
        // Force inaccuracy so the shooter really sprays
        activeStats.accuracy            = forcedAccuracy;
        activeStats.inaccuracyHalfAngle = forcedInaccuracyHalf;

        // Rebuild baseline interval from the previously applied multiplier,
        // then apply the NEW multiplier. This lets you move mult up OR down at runtime.
        float safePrevFR = Mathf.Max(0.01f, _prevFireRateMult);
        float baselineInterval = Mathf.Max(0.001f, activeStats.fireRate * safePrevFR);

        float safeNewFR = Mathf.Max(0.01f, fireRateMult);
        activeStats.fireRate = Mathf.Max(0.02f, baselineInterval / safeNewFR);
        _prevFireRateMult = safeNewFR;

        // Same idea for damage (baseline = current / prevMult)
        float safePrevDMG = Mathf.Max(0.0001f, _prevDamageMult);
        float baselineDamage = Mathf.Max(0f, activeStats.damage / safePrevDMG);

        float safeNewDMG = Mathf.Max(0.0001f, damageMult);
        activeStats.damage = baselineDamage * safeNewDMG;
        _prevDamageMult = safeNewDMG;
    }

    public override void OnBulletSpawn(Bullet bullet, WeaponStats activeStats)
    {
        var rb = bullet.GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
        if (rb) rb.linearDamping = bubbleDrag;
#else
        if (rb) rb.drag = bubbleDrag;
#endif
        bullet.transform.localScale *= sizeMult;

        if (sphereVisual) MakeSphereVisual(bullet);
    }

    static void MakeSphereVisual(Bullet bullet)
    {
        // Hide existing renderers (mesh can stay for collider/logic)
        var existing = bullet.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < existing.Length; i++) existing[i].enabled = false;

        // Add a simple sphere as the new look
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "CrabBubble_Sphere";
        Object.Destroy(sphere.GetComponent<Collider>()); // visual only
        sphere.transform.SetParent(bullet.transform, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale    = Vector3.one;

        var r = sphere.GetComponent<Renderer>();
        if (r)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null) mat = new Material(Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.8f, 0.95f, 1f, 1f));
            else if (mat.HasProperty("_Color")) mat.color = new Color(0.8f, 0.95f, 1f, 1f);
            r.material = mat;
        }
    }
}
