using UnityEngine;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

[CreateAssetMenu(menuName = "Geneforge/Abilities/Owl - Hunter's Mark")]
public class OwlHuntersMarkAbility : EssenceAbility
{
    [Header("Marking")]
    [Tooltip("Seconds. 0 or less = no expiry (persists until popped or target dies).")]
    public float markDuration = 8f;

    [Header("Pop")]
    public float popDamageFactor = 1.5f;     // x bullet damage
    public float splashRadius = 0f;          // optional AoE on pop (0 = none)
    public float splashDamageFactor = 0.5f;  // x pop damage to others
    [Tooltip("Cooldown after a mark pops before a new mark can be applied.")]
    public float popCooldownSeconds = 2f;

    [Header("Marker VFX")]
    public bool showMarker = true;
    public float markerRadius = 0.6f;
    public float markerWidth  = 0.06f;
    public Color markerColor  = new Color(1f, 0.9f, 0.2f, 1f);
    public float markerPulsePerSecond = 1.5f;  // ring breathes a bit
    public float markerHeadOffsetY = 0.15f;    // extra lift above top of renderers

    // --- state ---
    static Enemy  marked;
    static float  markExpireAt;
    static float  cooldownUntil;
    static MarkVFX currentVFX;

    public override void OnHitEnemy(Bullet bullet, Enemy enemy, WeaponStats stats)
    {
        float now = Time.time;

        // Pop if valid mark and shot hits it
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

            // clear + start cooldown
            ClearMark();
            cooldownUntil = now + Mathf.Max(0f, popCooldownSeconds);
            return;
        }

        // If on cooldown, do nothing
        if (now < cooldownUntil) return;

        // Otherwise (re)apply mark to this enemy
        ApplyMark(enemy);
    }

    void ApplyMark(Enemy enemy)
    {
        ClearMark();
        marked = enemy;
        markExpireAt = (markDuration > 0f) ? Time.time + markDuration : float.PositiveInfinity;

        if (showMarker && enemy != null)
        {
            currentVFX = MarkVFX.AttachAboveHead(
                enemy.transform, markerRadius, markerWidth, markerColor, markerPulsePerSecond, markDuration, markerHeadOffsetY
            );
            currentVFX.onDestroyed = () =>
            {
                // If VFX vanished due to expiry/death, clear mark as well
                if (marked == enemy)
                {
                    marked = null;
                    markExpireAt = 0f;
                }
            };
        }
    }

    void ClearMark()
    {
        marked = null;
        markExpireAt = 0f;
        if (currentVFX) Object.Destroy(currentVFX.gameObject);
        currentVFX = null;
    }

    // --- Head marker: pulsing circle that faces the camera at spawn, no further updates to facing ---
    class MarkVFX : MonoBehaviour
    {
        LineRenderer lr;
        float baseRadius;
        float width;
        Color color;
        float pulseHz;
        float endAt;   // <=0 => no timer

        public System.Action onDestroyed;

        public static MarkVFX AttachAboveHead(
            Transform target, float radius, float width, Color color, float pulsePerSecond, float duration, float extraY)
        {
            var go = new GameObject("Owl_Mark_VFX");
            // Parent first but keep world space so we can place it using world coords
            go.transform.SetParent(target, true);

            // Find top of the target's renderers/colliders
            Vector3 top = FindTopWorld(target);
            top.y += Mathf.Max(0f, extraY);

            go.transform.position = top;

            // Face the camera ONCE at spawn
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - go.transform.position;
                if (toCam.sqrMagnitude > 1e-4f)
                    go.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }

            var v = go.AddComponent<MarkVFX>();
            v.baseRadius = Mathf.Max(0.05f, radius);
            v.width      = Mathf.Max(0.01f, width);
            v.color      = color;
            v.pulseHz    = Mathf.Max(0f, pulsePerSecond);
            v.endAt      = (duration > 0f) ? Time.time + duration : 0f;
            v.Build();
            return v;
        }

        static Vector3 FindTopWorld(Transform t)
        {
            // Prefer renderers, then colliders, else fallback
            var rends = t.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
            var cols = t.GetComponentsInChildren<Collider>();
            if (cols != null && cols.Length > 0)
            {
                Bounds b = cols[0].bounds;
                for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
            return t.position + Vector3.up * 2f;
        }

        void Build()
        {
            lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 48;
            lr.widthMultiplier = width;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
        }

        void Update()
        {
            // pulse radius a little
            float r = baseRadius;
            if (pulseHz > 0f) r *= 1f + 0.08f * Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f);

            // Draw circle in LOCAL XY so the transform's facing (toward camera at spawn) is respected.
            for (int i = 0; i < lr.positionCount; i++)
            {
                float t = (i / (float)lr.positionCount) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(t) * r, Mathf.Sin(t) * r, 0f));
            }

            if (endAt > 0f && Time.time >= endAt) Destroy(gameObject);
        }

        void OnDestroy() { onDestroyed?.Invoke(); }
    }
}
