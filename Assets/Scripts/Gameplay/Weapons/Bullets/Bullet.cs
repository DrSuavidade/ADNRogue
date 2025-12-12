using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Weapons.Stats;
using Geneforge.Gameplay.Abilities;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Weapons.Bullets
{
    public class Bullet : MonoBehaviour
    {
        EnemyCore lastEnemyHit;

        [HideInInspector, SerializeField] private float damage = 1f;
        [HideInInspector, SerializeField] private float knockbackForce = 0f;
        [HideInInspector, SerializeField] private bool isCrit = false;

        [SerializeField] private float lifeTime = 3f;
        [SerializeField] private GameObject impactEffectPrefab;

        int pierceRemaining = 0;
        int bounceRemaining = 0;
        float homingStrength = 0f;
        float aoeRadius = 0f;

        [Header("Debug")]
        [SerializeField] bool showAoeRingOnHit = true;
        [SerializeField] LayerMask homingTargetMask = ~0;
        [SerializeField] float homingScanInterval = 0.1f;

        float _nextHomingScanTime;
        EnemyCore _cachedHomingTarget;
        Rigidbody rb;
        Collider myCol;
        Vector3 preStepVel;
        HashSet<EnemyCore> _hitEnemies =
        new HashSet<EnemyCore>();
        EssenceAbility _abilityAsset;
        WeaponStats _ws;
        PoolIdentifier poolId;

        // ---------------- Pool Reset Baseline ----------------
        bool _baselineCached;

        Vector3 _baseLocalScale;
        int _baseLayer;

        float _baseLifeTime;
        float _baseDamage;
        float _baseKnockback;
        bool _baseIsCrit;

        float _baseLinearDamping, _baseAngularDamping;

        Renderer[] _baseRenderers;
        bool[] _baseRendererEnabled;

        readonly HashSet<Collider> _ignoredColliders = new HashSet<Collider>();

        // ---------------- Chameleon Tongue Marker (no AddComponent) ----------------
        bool _hasTongue;
        Transform _tongueOwner;
        float _tongueDur;
        float _tongueForce;


        public float Damage
        {
            get => damage;
            set => damage = value;
        }

        public float KnockbackForce
        {
            get => knockbackForce;
            set => knockbackForce = value;
        }

        public bool IsCrit
        {
            get => isCrit;
            set => isCrit = value;
        }

        public float LifeTime
        {
            get => lifeTime;
            set => lifeTime = value;
        }

        public GameObject ImpactEffectPrefab
        {
            get => impactEffectPrefab;
            set => impactEffectPrefab = value;
        }


        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            myCol = GetComponent<Collider>();
            poolId = GetComponent<PoolIdentifier>();

            CacheBaseline();
        }

        void OnEnable()
        {
            // Pool can re-enable bullets; make sure they're always clean.
            if (_baselineCached)
                ResetForPool();
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
            ResetForPool();

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

        // --- Tongue marker API (used by Chameleon, safe for pooling) ---
        public void SetTongueMarker(Transform owner, float dur, float force)
        {
            _hasTongue = true;
            _tongueOwner = owner;
            _tongueDur = dur;
            _tongueForce = force;
        }

        public bool TryConsumeTongueMarker(out Transform owner, out float dur, out float force)
        {
            if (!_hasTongue)
            {
                owner = null; dur = 0f; force = 0f;
                return false;
            }

            owner = _tongueOwner;
            dur = _tongueDur;
            force = _tongueForce;

            _hasTongue = false;
            _tongueOwner = null;
            _tongueDur = 0f;
            _tongueForce = 0f;

            return true;
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
            var enemy = other.GetComponent<EnemyCore>();
            if (enemy != null) { HandleHitEnemy(enemy, other.ClosestPoint(transform.position)); return; }
            Despawn();
        }

        void OnCollisionEnter(Collision collision)
        {
            var enemy = collision.collider.GetComponent<EnemyCore>();
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

        EnemyCore FindBestHomingTarget()
        {
            const float radius = 12f;
            EnemyCore best = null;
            float bestDist = float.PositiveInfinity;

            var hits = Physics.OverlapSphere(transform.position, radius, homingTargetMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var e = hits[i].GetComponent<EnemyCore>();
                if (e == null) continue;
                float d = (e.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }

        void SteerTowards(EnemyCore best)
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

        void HandleHitEnemy(EnemyCore enemy, Vector3 hitPoint)
        {
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
                    var other = hits[i].GetComponent<EnemyCore>();
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

        void IgnoreEnemyColliders(EnemyCore enemy, bool ignore)
        {
            if (myCol == null || enemy == null) return;
            var cols = enemy.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) IgnoreCollisionTracked(cols[i], ignore);
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
                IgnoreCollisionTracked(other, true);
                yield return new WaitForSeconds(seconds);
                IgnoreCollisionTracked(other, false);
            }
        }

        // ---------------- Pool Reset Implementation ----------------
        void CacheBaseline()
        {
            if (_baselineCached) return;
            _baselineCached = true;

            _baseLocalScale = transform.localScale;
            _baseLayer = gameObject.layer;

            _baseLifeTime = lifeTime;
            _baseDamage = damage;
            _baseKnockback = knockbackForce;
            _baseIsCrit = isCrit;

            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                _baseLinearDamping = rb.linearDamping;
                _baseAngularDamping = rb.angularDamping;
#else
        _baseDrag = rb.drag;
        _baseAngularDrag = rb.angularDrag;
#endif
            }

            _baseRenderers = GetComponentsInChildren<Renderer>(true);
            _baseRendererEnabled = new bool[_baseRenderers.Length];
            for (int i = 0; i < _baseRenderers.Length; i++)
                _baseRendererEnabled[i] = _baseRenderers[i] && _baseRenderers[i].enabled;
        }

        void ResetForPool()
        {
            StopAllCoroutines();

            // Undo any Physics.IgnoreCollision calls made by this bullet
            UnignoreAll();

            // Clear ability bindings & runtime hit state
            _abilityAsset = null;
            _ws = null;
            _hitEnemies.Clear();
            lastEnemyHit = null;

            // Clear runtime projectile modifiers
            pierceRemaining = 0;
            bounceRemaining = 0;
            homingStrength = 0f;
            aoeRadius = 0f;
            _cachedHomingTarget = null;
            _nextHomingScanTime = 0f;

            // Clear tongue marker (Chameleon)
            _hasTongue = false;
            _tongueOwner = null;
            _tongueDur = 0f;
            _tongueForce = 0f;

            // Restore baseline fields
            lifeTime = _baseLifeTime;
            damage = _baseDamage;
            knockbackForce = _baseKnockback;
            isCrit = _baseIsCrit;

            // Restore transform + layer
            transform.localScale = _baseLocalScale;
            gameObject.layer = _baseLayer;

            // Restore rigidbody damping + velocity
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.linearDamping = _baseLinearDamping;
                rb.angularDamping = _baseAngularDamping;
#else
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.drag = _baseDrag;
        rb.angularDrag = _baseAngularDrag;
#endif
            }

            // Restore renderers (fix Crab disabling them)
            if (_baseRenderers != null)
            {
                for (int i = 0; i < _baseRenderers.Length; i++)
                {
                    var r = _baseRenderers[i];
                    if (!r) continue;
                    r.enabled = _baseRendererEnabled[i];
                }
            }

            // Disable Crab sphere if present (prevents visual state leak without Destroy timing issues)
            var bubble = transform.Find("CrabBubble_Sphere");
            if (bubble != null) bubble.gameObject.SetActive(false);
        }

        void IgnoreCollisionTracked(Collider other, bool ignore)
        {
            if (!myCol || !other) return;

            Physics.IgnoreCollision(myCol, other, ignore);

            if (ignore) _ignoredColliders.Add(other);
            else _ignoredColliders.Remove(other);
        }

        void UnignoreAll()
        {
            if (!myCol) { _ignoredColliders.Clear(); return; }

            foreach (var c in _ignoredColliders)
                if (c) Physics.IgnoreCollision(myCol, c, false);

            _ignoredColliders.Clear();
        }
    }
}
