using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Characters.Enemies.Ranged;
using Geneforge.Core.Pooling;

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
        int pierceRemaining = 0;
        int bounceRemaining = 0;
        float homingStrength = 0f;
        float aoeRadius = 0f;

        [Header("Debug")]
        [SerializeField] bool showAoeRingOnHit = true;
        [SerializeField] LayerMask homingTargetMask = ~0;
        [SerializeField] float homingScanInterval = 0.1f;

        float _nextHomingScanTime;
        Enemy _cachedHomingTarget;
        Rigidbody rb;
        Collider myCol;
        Vector3 preStepVel;
        HashSet<Enemy> _hitEnemies =
        new HashSet<Enemy>();
        EssenceAbility _abilityAsset;
        WeaponStats _ws;
        PoolIdentifier poolId;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            myCol = GetComponent<Collider>();
            poolId = GetComponent<PoolIdentifier>();
        }

        void Update()
        {
            if (homingStrength <= 0f || rb == null) return;

            if (Time.time >= _nextHomingScanTime)
            {
                _cachedHomingTarget = FindBestHomingTarget();
                _nextHomingScanTime = Time.time + homingScanInterval;
            }

            if (_cachedHomingTarget == null) return;
            SteerTowards(_cachedHomingTarget);
        }

        void FixedUpdate()
        {
#if UNITY_6000_0_OR_NEWER
            preStepVel = rb.linearVelocity;
#else
            preStepVel = rb.velocity;
#endif
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

            StopAllCoroutines();
            StartCoroutine(DieAfter(lifeTime));
        }

        void Despawn()
        {
            StopAllCoroutines();

            _abilityAsset = null;
            _ws = null;
            _hitEnemies.Clear();
            lastEnemyHit = null;
            pierceRemaining = 0;
            bounceRemaining = 0;
            homingStrength = 0f;
            aoeRadius = 0f;

            if (poolId != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.Reclaim(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void BindAbility(EssenceAbility ability, WeaponStats stats)
        {
            _abilityAsset = ability;
            _ws = stats;

            _abilityAsset?.OnBulletSpawn(this, _ws);
        }


        IEnumerator DieAfter(float t) { yield return new WaitForSeconds(t); Despawn(); }

        public void ApplyRuntimeStats(WeaponStats ws)
        {
            if (ws == null) return;
            lifeTime = ws.ProjectileLifetime;
            pierceRemaining = ws.PierceCount;
            bounceRemaining = ws.BounceCount;
            homingStrength = Mathf.Clamp01(ws.HomingStrength);
            aoeRadius = Mathf.Max(0f, ws.AoeRadius);
        }


        // ---------------- Collisions: support trigger or non-trigger ----------------
        void OnTriggerEnter(Collider other)
        {
            var enemy = other.GetComponent<Enemy>();
            if (enemy != null) { HandleHitEnemy(enemy, other.ClosestPoint(transform.position)); return; }
            Despawn();
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

            if (bounceRemaining > 0 && rb != null && collision.contactCount > 0)
            {
                ContactPoint cp = collision.GetContact(0);
                Vector3 n = cp.normal.normalized;

                Vector3 vIn = preStepVel;
                float speed = Mathf.Max(vIn.magnitude, 0.1f);

                Vector3 dirOut = Vector3.Reflect(vIn.normalized, n);

                DepenetrateFrom(collision.collider, n, cp.point);

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = dirOut * speed;
#else
                rb.velocity = dirOut * speed;
#endif
                rb.angularVelocity = Vector3.zero;
                transform.forward = dirOut;

                StartCoroutine(TemporarilyIgnore(collision.collider, 0.06f));

                bounceRemaining--;
                return;
            }

            Despawn();
        }

        void DepenetrateFrom(Collider other, Vector3 fallbackNormal, Vector3 contactPoint)
        {
            if (myCol == null || other == null) return;

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
                transform.position = contactPoint + fallbackNormal.normalized * 0.02f;
            }
        }

        Enemy FindBestHomingTarget()
        {
            const float radius = 12f;
            Enemy best = null;
            float bestDist = float.PositiveInfinity;

            var hits = Physics.OverlapSphere(transform.position, radius, homingTargetMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var e = hits[i].GetComponent<Enemy>();
                if (e == null) continue;
                float d = (e.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }

        void SteerTowards(Enemy best)
        {
            Vector3 desired = (best.transform.position - transform.position).normalized;
#if UNITY_6000_0_OR_NEWER
            Vector3 curDir = rb.linearVelocity.sqrMagnitude > 1e-6f ? rb.linearVelocity.normalized : transform.forward;
            float speed = rb.linearVelocity.magnitude;
#else
            Vector3 curDir  = rb.velocity.sqrMagnitude > 1e-6f ? rb.velocity.normalized : transform.forward;
            float speed     = rb.velocity.magnitude;
#endif

            float turnRad = (360f * Mathf.Deg2Rad) * Mathf.Clamp01(homingStrength) * Time.deltaTime;

            Vector3 newDir = Vector3.RotateTowards(curDir, desired, turnRad, 0f);
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = newDir * speed;
#else
            rb.velocity = newDir * speed;
#endif
            transform.forward = newDir;
        }

        void HandleHitEnemy(Enemy enemy, Vector3 hitPoint)
        {
            var rangedMagic = enemy.GetComponent<RangedMagic>();
            if (rangedMagic != null && rangedMagic.IsBlocking)
            {
                Despawn();
                return;
            }

            if (_hitEnemies.Contains(enemy)) return;
            _hitEnemies.Add(enemy);

            enemy.TakeDamage(damage, isCrit);
            lastEnemyHit = enemy;
            if (knockbackForce > 0f)
            {
                Vector3 dir = transform.forward; dir.y = 0f;
                enemy.ApplyKnockback(dir.normalized, knockbackForce);
            }

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

            _abilityAsset?.OnHitEnemy(this, enemy, _ws);

            IgnoreEnemyColliders(enemy, true);

            if (pierceRemaining > 0)
            {
                pierceRemaining--;

                if (rb)
                {
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

                return;
            }

            Despawn();
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

            var go = new GameObject("AoE_Debug_Ring");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 64;
            lr.widthMultiplier = 0.05f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(1f, 1f, 1f, 0.7f);

            Vector3[] pts = new Vector3[lr.positionCount];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = (i / (float)pts.Length) * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            }
            lr.SetPositions(pts);

            go.transform.rotation = Quaternion.identity;

            Destroy(go, seconds);

            float tSec = 0f;
            while (tSec < seconds && target != null)
            {
                Vector3 p = target.position;
                go.transform.position = ProjectToGround(p) + Vector3.up * 0.02f;

                tSec += Time.deltaTime;
                yield return null;
            }

            if (lr != null && lr.material != null) Destroy(lr.material);
            if (go) Destroy(go);
        }

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
    }
}
