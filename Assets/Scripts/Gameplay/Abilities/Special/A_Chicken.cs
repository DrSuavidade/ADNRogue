using UnityEngine;
using System.Collections.Generic;
using Geneforge.Gameplay.Weapons.Bullets;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Abilities.Special
{
    [CreateAssetMenu(menuName = "Geneforge/Abilities/Chicken - Beak Cone")]
    public class ChickenBeakConeAbility : EssenceAbility
    {
        [Header("Cone")]
        [Range(1f, 180f)] public float coneAngle = 35f;
        public float coneRange = 6f;

        [Header("Damage/FX")]
        public float damageFactor = 1.0f;
        [Range(0f, 1f)] public float hitFalloff = 0f;
        public float knockback = 0f;

        [Header("VFX")]
        public bool showVfx = true;
        public float vfxDuration = 0.15f;
        public float vfxLineWidth = 0.06f;
        public int vfxSegments = 32;
        public Color vfxColor = new Color(1f, 0.9f, 0.4f, 0.9f);
        public float vfxBaseOffset = 0.05f;

        public override void OnBulletSpawn(Bullet bullet, WeaponStats stats)
        {
            Vector3 origin = bullet.transform.position;
            Vector3 fwd = bullet.transform.forward;

            var hits = Physics.OverlapSphere(origin, coneRange, ~0, QueryTriggerInteraction.Ignore);
            var done = new HashSet<EnemyCore>();

            for (int i = 0; i < hits.Length; i++)
            {
                var e = hits[i].GetComponent<EnemyCore>();
                if (e == null || done.Contains(e)) continue;

                Vector3 to = e.transform.position - origin;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < 0.001f) continue;

                float ang = Vector3.Angle(new Vector3(fwd.x, 0f, fwd.z), to.normalized);
                if (ang <= coneAngle * 0.5f && dist <= coneRange)
                {
                    float dmg = stats.Damage * damageFactor;
                    if (hitFalloff > 0f)
                    {
                        float t = Mathf.Clamp01(dist / coneRange);
                        dmg *= Mathf.Lerp(1f, 1f - hitFalloff, t);
                    }

                    e.TakeDamage(dmg, false);

                    if (knockback > 0f)
                    {
                        var dir = to.normalized; dir.y = 0f;
                        e.ApplyKnockback(dir, knockback);
                    }

                    done.Add(e);
                }
            }

            if (showVfx) SpawnBeakTriangleVFX(origin, fwd, coneRange, coneAngle, vfxLineWidth, vfxColor, vfxDuration, vfxBaseOffset);


            Destroy(bullet.gameObject);
        }

        static void SpawnBeakTriangleVFX(
        Vector3 origin, Vector3 forward, float range, float angleDeg,
        float width, Color color, float life, float baseOffset)
        {
            var go = new GameObject("ChickenBeak_TriangleVFX");
            Vector3 flatFwd = new Vector3(forward.x, 0f, forward.z).normalized;
            if (flatFwd.sqrMagnitude < 1e-4f) flatFwd = Vector3.forward;
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(flatFwd, Vector3.up));

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 3;
            lr.widthMultiplier = width;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;

            float halfRad = Mathf.Deg2Rad * (angleDeg * 0.5f);
            float halfBase = Mathf.Tan(halfRad) * range;

            float zBase = Mathf.Max(0f, baseOffset);

            Vector3 leftBase = new Vector3(-halfBase, 0f, zBase);
            Vector3 rightBase = new Vector3(+halfBase, 0f, zBase);
            Vector3 apex = new Vector3(0f, 0f, range);

            lr.SetPosition(0, leftBase);
            lr.SetPosition(1, apex);
            lr.SetPosition(2, rightBase);

            go.AddComponent<FadeAndDie>().Init(lr, color, Mathf.Max(0.02f, life));
        }


        class FadeAndDie : MonoBehaviour
        {
            LineRenderer lr;
            Color baseCol;
            float life;
            float t;

            public void Init(LineRenderer _lr, Color c, float _life)
            {
                lr = _lr; baseCol = c; life = Mathf.Max(0.02f, _life);
            }

            void Update()
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / life);
                var c = baseCol; c.a *= k;
                lr.startColor = c; lr.endColor = c;

                if (t >= life) Destroy(gameObject);
            }
        }
        public override void ApplyUpgrades(AbilityUpgrade[] upgrades)
        {
            if (upgrades == null) return;

            for (int i = 0; i < upgrades.Length; i++)
            {
                var u = upgrades[i];
                switch (u.key)
                {
                    case "Beak/ConeAngle":
                        coneAngle = Mathf.Clamp(ApplyNumeric(coneAngle, u), 1f, 180f);
                        break;

                    case "Beak/ConeRange":
                        coneRange = Mathf.Max(0.5f, ApplyNumeric(coneRange, u));
                        break;

                    case "Beak/DamageFactor":
                        damageFactor = Mathf.Max(0f, ApplyNumeric(damageFactor, u));
                        break;

                    case "Beak/HitFalloff":
                        hitFalloff = Mathf.Clamp01(ApplyNumeric(hitFalloff, u));
                        break;

                    case "Beak/Knockback":
                        knockback = Mathf.Max(0f, ApplyNumeric(knockback, u));
                        break;
                }
            }
        }
    }
}