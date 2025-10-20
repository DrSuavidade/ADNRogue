using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Weapons.Stats;

namespace Geneforge.Gameplay.Weapons.Bullets
{
    public class Bullet : MonoBehaviour
    {
        Enemy lastEnemyHit;

        [HideInInspector] public float damage = 1f;
        [HideInInspector] public float knockbackForce = 0f;
        [HideInInspector] public bool isCrit = false;

        public float lifeTime = 3f;
        public GameObject impactEffectPrefab;

        // Runtime behavior from WeaponStats
        int pierceRemaining = 0;
        int bounceRemaining = 0;
        float homingStrength = 0f;
        float aoeRadius = 0f;

        // Debug visual for AoE (testing)
        [Header("Debug")]
        [SerializeField] bool showAoeRingOnHit = true;

        Rigidbody rb;
        Collider myCol;
        Vector3 preStepVel;
        HashSet<Enemy> _hitEnemies =
        new HashSet<Enemy>();


        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            myCol = GetComponent<Collider>();
        }

        // Minimal homing: uses only homingStrength
        void Update()
        {
            if (homingStrength <= 0f || rb == null) return;

            // find nearest enemy each frame (simple + robust)
            const float radius = 12f;
            Geneforge.Gameplay.Characters.Enemies.Enemy best = null;
            float bestDist = float.PositiveInfinity;

            foreach (var col in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var e = col.GetComponent<Geneforge.Gameplay.Characters.Enemies.Enemy>();
                if (e == null) continue;
                float d = (e.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            if (best == null) return;

            // steer current velocity toward target
            Vector3 desired = (best.transform.position - transform.position).normalized;
#if UNITY_6000_0_OR_NEWER
            Vector3 curDir = rb.linearVelocity.sqrMagnitude > 1e-6f ? rb.linearVelocity.normalized : transform.forward;
            float speed = rb.linearVelocity.magnitude;
#else
            Vector3 curDir  = rb.velocity.sqrMagnitude > 1e-6f ? rb.velocity.normalized : transform.forward;
            float speed     = rb.velocity.magnitude;
#endif

            // turn rate scales with homingStrength (0..1). 360°/s at strength=1.
            float turnRad = (360f * Mathf.Deg2Rad) * Mathf.Clamp01(homingStrength) * Time.deltaTime;

            Vector3 newDir = Vector3.RotateTowards(curDir, desired, turnRad, 0f);
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = newDir * speed;
#else
            rb.velocity = newDir * speed;
#endif
            transform.forward = newDir;
        }


        void FixedUpdate()
        {
            preStepVel = rb.linearVelocity;
        }

        public void Launch(Vector3 dir, float speed)
        {
            if (rb)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = dir.normalized * speed;
#else
                rb.velocity = dir.normalized * speed;
#endif
                transform.forward = dir.normalized;
            }
            else transform.forward = dir.normalized;

            // lifetime
            StopAllCoroutines();
            StartCoroutine(DieAfter(lifeTime));
        }

        IEnumerator DieAfter(float t) { yield return new WaitForSeconds(t); Destroy(gameObject); }

        public void ApplyRuntimeStats(WeaponStats ws)
        {
            if (ws == null) return;
            lifeTime = ws.projectileLifetime;
            pierceRemaining = ws.pierceCount;
            bounceRemaining = ws.bounceCount;
            homingStrength = Mathf.Clamp01(ws.homingStrength);
            aoeRadius = Mathf.Max(0f, ws.aoeRadius);
        }

        // ---------------- Collisions: support trigger or non-trigger ----------------
        void OnTriggerEnter(Collider other)
        {
            // If your bullets/enemies use triggers, handle here
            var enemy = other.GetComponent<Enemy>();
            if (enemy != null) { HandleHitEnemy(enemy, other.ClosestPoint(transform.position)); return; }
            // No bounce on trigger surfaces (no normal) — just destroy unless you want special cases
        }

        void OnCollisionEnter(Collision collision)
        {
            var enemy = collision.collider.GetComponent<Enemy>();
            var point = (collision.contacts.Length > 0) ? collision.contacts[0].point : transform.position;

            if (enemy != null)
            {
                HandleHitEnemy(enemy, point);
                return;
            }

            // Environment bounce (specular mirror about the surface normal)
            if (bounceRemaining > 0 && rb != null && collision.contactCount > 0)
            {
                // Use the first contact's normal (clean mirror off the actual surface)
                ContactPoint cp = collision.GetContact(0);
                Vector3 n = cp.normal.normalized;

                // The incoming velocity BEFORE physics resolved this contact (captured in FixedUpdate)
                Vector3 vIn = preStepVel;
                float speed = Mathf.Max(vIn.magnitude, 0.1f);

                // Mirror reflection: angle out == angle in relative to the perpendicular (normal)
                Vector3 dirOut = Vector3.Reflect(vIn.normalized, n);

                // Move the bullet slightly out of the surface so it doesn't immediately re-collide
                DepenetrateFrom(collision.collider, n, cp.point);

                // Apply reflected motion & orientation
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = dirOut * speed;
#else
                rb.velocity = dirOut * speed;
#endif
                rb.angularVelocity = Vector3.zero;
                transform.forward = dirOut;

                // Avoid an immediate second hit on the same face
                StartCoroutine(TemporarilyIgnore(collision.collider, 0.06f));

                bounceRemaining--;
                return;
            }


            Destroy(gameObject);
        }

        void DepenetrateFrom(Collider other, Vector3 fallbackNormal, Vector3 contactPoint)
        {
            if (myCol == null || other == null) return;

            // Best case: exact minimal translation using ComputePenetration
            Vector3 dir; float dist;
            if (Physics.ComputePenetration(
                    myCol, transform.position, transform.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out dir, out dist))
            {
                transform.position += dir * (dist + 0.005f);
            }
            else
            {
                // Fallback: small push along the contact normal from the contact point
                transform.position = contactPoint + fallbackNormal.normalized * 0.02f;
            }
        }


        void HandleHitEnemy(Enemy enemy, Vector3 hitPoint)
        {
            if (_hitEnemies.Contains(enemy)) return;
            _hitEnemies.Add(enemy);

            // Damage & knockback
            enemy.TakeDamage(damage, isCrit);
            lastEnemyHit = enemy;
            if (knockbackForce > 0f)
            {
                Vector3 dir = transform.forward; dir.y = 0f;
                enemy.ApplyKnockback(dir.normalized, knockbackForce);
            }

            // AoE splash + simple debug ring
            if (aoeRadius > 0f)
            {
                var hits = Physics.OverlapSphere(hitPoint, aoeRadius);
                for (int i = 0; i < hits.Length; i++)
                {
                    var other = hits[i].GetComponent<Enemy>();
                    if (other != null && other != enemy) other.TakeDamage(damage, false);
                }

                if (showAoeRingOnHit) StartCoroutine(AoeRingFollow(enemy.transform, aoeRadius, 0.5f));
            }

            // From now on, never collide with this enemy again (all colliders in its hierarchy)
            IgnoreEnemyColliders(enemy, true);

            // --- Pierce path: keep going straight, no spin, outside the enemy ---
            if (pierceRemaining > 0)
            {
                pierceRemaining--;

                if (rb)
                {
                    // restore clean motion from BEFORE impact
                    Vector3 dir = preStepVel.sqrMagnitude > 1e-6f ? preStepVel.normalized : transform.forward;
                    float spd = Mathf.Max(preStepVel.magnitude, 0.01f);
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = dir * spd;
#else
                    rb.velocity = dir * spd;
#endif
                    rb.angularVelocity = Vector3.zero;
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }

                // ensure we’re not overlapping the enemy anymore
                // try exact depenetration; else nudge forward a bit
                Vector3 depenDir; float depenDist;
                var enemyRootCol = enemy.GetComponentInChildren<Collider>();
                if (myCol != null && enemyRootCol != null &&
                    Physics.ComputePenetration(myCol, transform.position, transform.rotation,
                                            enemyRootCol, enemyRootCol.transform.position, enemyRootCol.transform.rotation,
                                            out depenDir, out depenDist))
                {
                    transform.position += depenDir * (depenDist + 0.005f);
                }
                else
                {
                    transform.position = hitPoint + transform.forward * 0.05f;
                }

                return; // continue flying
            }

            Destroy(gameObject);
        }

        void IgnoreEnemyColliders(Geneforge.Gameplay.Characters.Enemies.Enemy enemy, bool ignore)
        {
            if (myCol == null || enemy == null) return;
            var cols = enemy.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) Physics.IgnoreCollision(myCol, cols[i], ignore);
            }
        }


        IEnumerator AoeRingFollow(Transform target, float radius, float seconds)
        {
            if (target == null) yield break;

            // create ring
            var go = new GameObject("AoE_Debug_Ring");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;                  // local circle; we'll move the GO
            lr.loop = true;
            lr.positionCount = 64;
            lr.widthMultiplier = 0.05f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(1f, 1f, 1f, 0.7f);

            // build local-space circle ON XZ PLANE (parallel to ground)
            Vector3[] pts = new Vector3[lr.positionCount];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = (i / (float)pts.Length) * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            }
            lr.SetPositions(pts);

            // ensure orientation parallel to Y=0
            go.transform.rotation = Quaternion.identity;

            // hard-destroy after 'seconds' no matter what (even if this MonoBehaviour dies)
            Destroy(go, seconds);

            float tSec = 0f;
            while (tSec < seconds && target != null)
            {
                // place near the enemy's feet (ground-projected)
                Vector3 p = target.position;
                go.transform.position = ProjectToGround(p) + Vector3.up * 0.02f;

                // no spin, stay parallel to ground
                // go.transform.rotation = Quaternion.identity; // not needed each frame, but harmless

                tSec += Time.deltaTime;
                yield return null;
            }

            // if the coroutine is still alive, clean material + GO explicitly
            if (lr != null && lr.material != null) Destroy(lr.material);
            if (go) Destroy(go);
        }

        // helper: project to ground under a point
        Vector3 ProjectToGround(Vector3 around)
        {
            if (Physics.Raycast(around + Vector3.up * 2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(around.x, 0f, around.z);
        }


        IEnumerator TemporarilyIgnore(Collider other, float seconds)
        {
            if (myCol && other)
            {
                Physics.IgnoreCollision(myCol, other, true);
                yield return new WaitForSeconds(seconds);
                if (myCol && other) Physics.IgnoreCollision(myCol, other, false);
            }
        }

        IEnumerator AoeRing(Vector3 center, float radius, float seconds)
        {
            // cheap, no-alloc debug ring using a temporary primitive
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = center;
            go.transform.localScale = Vector3.one * (radius * 2f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(1f, 1f, 1f, 0.1f);
            yield return new WaitForSeconds(seconds);
            if (go) Destroy(go);
        }

        Vector3 GetVelocityDir()
        {
            if (!rb) return transform.forward;
#if UNITY_6000_0_OR_NEWER
            var v = rb.linearVelocity;
#else
            var v = rb.velocity;
#endif
            return v.sqrMagnitude > 1e-6f ? v.normalized : transform.forward;
        }
    }
}
