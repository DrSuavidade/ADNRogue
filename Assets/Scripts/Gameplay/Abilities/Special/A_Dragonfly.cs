using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Dragonfly - Vector Lock")]
public class A_DragonflyVectorLock : EssenceAbility
{
    [Header("Speed & Steering")]
    public float        speedMultiplier = 1.35f;         // multiply initial speed
    [Range(0f, 1f)] public float homingAdd = 0.35f;      // extra steer (0..1) -> 360°/s * value
    public float        seekRadius      = 14f;           // how far to look for targets

    [Header("Trail (optional)")]
    public bool  addTrail   = true;
    public float trailTime  = 0.25f;
    public float trailWidth = 0.08f;

    public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
    {
        // Speed up the already-launched rigidbody
        var rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity *= speedMultiplier;
#else
            rb.velocity *= speedMultiplier;
#endif
        }

        // Add extra steering without touching Bullet internals
        if (homingAdd > 0f)
        {
            var steer = bullet.gameObject.AddComponent<ExtraHomingSteer>();
            steer.turnRateDegPerSec = 360f * Mathf.Clamp01(homingAdd);
            steer.seekRadius        = seekRadius;
        }

        if (addTrail) AttachTrail(bullet);
    }

    // Adds extra homing without touching Bullet internals
    class ExtraHomingSteer : MonoBehaviour
    {
        public float turnRateDegPerSec = 120f;
        public float seekRadius        = 12f;
        Rigidbody rb;

        void Awake() { rb = GetComponent<Rigidbody>(); }

        void Update()
        {
            if (!rb) return;
            var cols = Physics.OverlapSphere(transform.position, seekRadius, ~0, QueryTriggerInteraction.Ignore);

            Geneforge.Gameplay.Characters.Enemies.Enemy best = null;
            float bestD = float.PositiveInfinity;

            for (int i = 0; i < cols.Length; i++)
            {
                var e = cols[i].GetComponent<Geneforge.Gameplay.Characters.Enemies.Enemy>();
                if (!e) continue;
                float d = (e.transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = e; }
            }
            if (!best) return;

#if UNITY_6000_0_OR_NEWER
            Vector3 v = rb.linearVelocity;
#else
            Vector3 v = rb.velocity;
#endif
            float speed = v.magnitude;
            if (speed < 1e-3f) return;

            Vector3 cur = v / speed;
            Vector3 desired = (best.transform.position - transform.position).normalized;
            float maxRad = Mathf.Deg2Rad * turnRateDegPerSec * Time.deltaTime;
            Vector3 newDir = Vector3.RotateTowards(cur, desired, maxRad, 0f);

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = newDir * speed;
#else
            rb.velocity = newDir * speed;
#endif
            transform.forward = newDir;
        }
    }

    void AttachTrail(Bullet bullet)
    {
        var tr = bullet.GetComponent<TrailRenderer>();
        if (!tr) tr = bullet.gameObject.AddComponent<TrailRenderer>();
        tr.time = Mathf.Max(0.01f, trailTime);
        tr.widthMultiplier = Mathf.Max(0.005f, trailWidth);
        tr.minVertexDistance = 0.01f;
        tr.alignment = LineAlignment.View;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        // Simple purple gradient
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.8f, 0.4f, 1f), 0f),
                new GradientColorKey(new Color(0.6f, 0.2f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        tr.colorGradient = g;

        if (!tr.material) tr.material = new Material(Shader.Find("Sprites/Default"));
        tr.sortingLayerID = bullet.gameObject.layer; // keep on same render layer/culling
    }
    public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
    {
        if (upgrades == null) return;

        for (int i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            switch (u.key)
            {
                case "Dragonfly/SpeedMultiplier":
                    speedMultiplier = Mathf.Max(0f, ApplyNumeric(speedMultiplier, u));
                    break;

                case "Dragonfly/HomingAdd":
                    homingAdd = Mathf.Clamp01(ApplyNumeric(homingAdd, u));
                    break;

                case "Dragonfly/SeekRadius":
                    seekRadius = Mathf.Max(0f, ApplyNumeric(seekRadius, u));
                    break;

                case "Dragonfly/TrailTime":
                    trailTime = Mathf.Max(0f, ApplyNumeric(trailTime, u));
                    break;

                case "Dragonfly/TrailWidth":
                    trailWidth = Mathf.Max(0.001f, ApplyNumeric(trailWidth, u));
                    break;
            }
        }
    }

}
