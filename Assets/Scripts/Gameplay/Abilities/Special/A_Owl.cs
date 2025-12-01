using UnityEngine;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Abilities.Special
{
    [CreateAssetMenu(menuName = "Geneforge/Abilities/Owl - Hunter's Mark")]
    public class OwlHuntersMarkAbility : EssenceAbility
    {
        [Header("Marking")]
        [Tooltip("Seconds. 0 or less = no expiry (persists until popped or target dies).")]
        public float markDuration = 8f;

        [Header("Pop")]
        public float popDamageFactor = 1.5f;
        public float splashRadius = 0f;
        public float splashDamageFactor = 0.5f;
        [Tooltip("Cooldown after a mark pops before a new mark can be applied.")]
        public float popCooldownSeconds = 2f;

        [Header("Marker VFX")]
        public bool showMarker = true;
        public float markerRadius = 0.6f;
        public float markerWidth = 0.06f;
        public Color markerColor = new Color(1f, 0.9f, 0.2f, 1f);
        public float markerPulsePerSecond = 1.5f;
        public float markerHeadOffsetY = 0.15f;
        static EnemyCore marked;
        static float markExpireAt;
        static float cooldownUntil;
        static MarkVFX currentVFX;

        public override void OnHitEnemy(Bullet bullet, EnemyCore enemy, WeaponStats stats)
        {
            float now = Time.time;

            if (marked != null && enemy == marked && (markDuration <= 0f || now <= markExpireAt))
            {
                float popDmg = stats.Damage * popDamageFactor;
                enemy.TakeDamage(popDmg, false);

                if (splashRadius > 0f)
                {
                    var cols = Physics.OverlapSphere(enemy.transform.position, splashRadius, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < cols.Length; i++)
                    {
                        var e2 = cols[i].GetComponent<EnemyCore>();
                        if (e2 != null && e2 != enemy) e2.TakeDamage(popDmg * splashDamageFactor, false);
                    }
                }

                ClearMark();
                cooldownUntil = now + Mathf.Max(0f, popCooldownSeconds);
                return;
            }

            if (now < cooldownUntil) return;

            ApplyMark(enemy);
        }

        void ApplyMark(EnemyCore enemy)
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


        class MarkVFX : MonoBehaviour
        {
            LineRenderer lr;
            float baseRadius;
            float width;
            Color color;
            float pulseHz;
            float endAt;

            public System.Action onDestroyed;

            public static MarkVFX AttachAboveHead(
                Transform target, float radius, float width, Color color, float pulsePerSecond, float duration, float extraY)
            {
                var go = new GameObject("Owl_Mark_VFX");
                go.transform.SetParent(target, true);

                Vector3 top = FindTopWorld(target);
                top.y += Mathf.Max(0f, extraY);

                go.transform.position = top;

                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 toCam = cam.transform.position - go.transform.position;
                    if (toCam.sqrMagnitude > 1e-4f)
                        go.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                }

                var v = go.AddComponent<MarkVFX>();
                v.baseRadius = Mathf.Max(0.05f, radius);
                v.width = Mathf.Max(0.01f, width);
                v.color = color;
                v.pulseHz = Mathf.Max(0f, pulsePerSecond);
                v.endAt = (duration > 0f) ? Time.time + duration : 0f;
                v.Build();
                return v;
            }

            static Vector3 FindTopWorld(Transform t)
            {
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
                float r = baseRadius;
                if (pulseHz > 0f) r *= 1f + 0.08f * Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f);

                for (int i = 0; i < lr.positionCount; i++)
                {
                    float t = (i / (float)lr.positionCount) * Mathf.PI * 2f;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(t) * r, Mathf.Sin(t) * r, 0f));
                }

                if (endAt > 0f && Time.time >= endAt) Destroy(gameObject);
            }

            void OnDestroy() { onDestroyed?.Invoke(); }
        }
        public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
        {
            if (upgrades == null) return;

            for (int i = 0; i < upgrades.Length; i++)
            {
                var u = upgrades[i];
                switch (u.key)
                {
                    case "Owl/MarkDuration":
                        markDuration = Mathf.Max(0.1f, ApplyNumeric(markDuration, u));
                        break;

                    case "Owl/PopDamageFactor":
                        popDamageFactor = Mathf.Max(0f, ApplyNumeric(popDamageFactor, u));
                        break;

                }
            }
        }
    }
}
