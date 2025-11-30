using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Geneforge.Gameplay.Characters.Enemies.Suporte
{
    public class Suporte : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;

        [Header("Comportamento")]
        public bool stayStationary = true;
        public bool rotateInPlace = true;

        [Header("Wander Settings")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;
        public float idleWaitDuration = 1f;

        [Header("Deteção / Mirar")]
        public float detectionRadius = 20f;
        public float attackRange = 10f;
        public float attackRate = 1f;

        [Header("Escudo")]
        public float shieldDuration = 5f;
        public float shieldRadius = 8f;
        public int maxAlliesPerCast = 0;

        [Header("Visual do Círculo")]
        public Vector3 indicatorOffset = new Vector3(0f, 2.0f, 0f);
        public float circleRadius = 0.5f;
        public int circleSegments = 24;
        public float circleWidth = 0.05f;

        [Header("Layer (Opcional para reforço)")]
        [Tooltip("Layer que os ataques do jogador ignoram (ex.: 'Shielded'). Deixa -1 para não trocar.")]
        public int shieldedLayer = -1;

        [Header("Feedback a Dano")]
        public float damagePauseDuration = 0.5f;

        // ------- Lock / Motion Control -------
        [Header("Motion Control")]
        public bool disableRootMotionWhenLocked = true;
        public bool hardStopRigidbodyWhenLocked = true;
        public bool hardStopFreezeRotationY = true;

        [Header("Attack Lock / Detection")]
        public bool freezeRotationWhileAttacking = true;
        public string[] attackStateNames = { "Attack", "AttackB", "AttackC" };
        public string[] attackStateTags  = { "Attack" };

        [Header("Hit Lock / Detection")]
        public bool freezeRotationWhileHit = true;
        public string[] hitStateNames = { "Damaged", "Hit", "Hurt" };
        public string[] hitStateTags  = { "Hurt", "Damaged" };
        // ------------------------------------

        // --- Estado interno ---
        Vector3 spawnPos, wanderTarget;
        float wanderTimer, lastAttackTime;
        bool isIdleWaiting = false;
        float idleWaitTimer = 0f;

        enum State { Wandering, Attacking }
        State state = State.Wandering;

        Transform player;
        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        static readonly Collider[] overlapBuffer = new Collider[96];

        // ---- Refs auxiliares ----
        Geneforge.Gameplay.Characters.Enemies.Enemy enemy;
        Rigidbody _rb;
        CharacterController _cc;

        // Lock de translação XZ
        bool _translationLocked = false;
        Vector3 _pinnedXZ;
        RigidbodyConstraints _rbPrevConstraints;
        bool _rbHadConstraints = false;

        // Pin de rotação durante ataque/hit
        bool _facingPinned = false;
        Quaternion _pinnedFacing;

        // ----- Escudo (dados por-alvo) -----
        class ShieldData
        {
            public Transform root;
            public float endTime;

            public Rigidbody rb;
            public bool prevIsKinematic;

            public CharacterController cc;
            public bool prevCCEnabled;

            public List<Collider> colliders = new List<Collider>(32);
            public List<bool> prevEnabled   = new List<bool>(32);

            public Dictionary<Transform,int> originalLayers = new Dictionary<Transform,int>(64);
            public string originalTag;

            public GameObject circleObj;
            public LineRenderer lr;
        }
        readonly Dictionary<Transform, ShieldData> activeShields = new Dictionary<Transform, ShieldData>();

        // ----- Morte / Despawn -----
        bool _deathFrozen = false;
        bool _despawnStarted = false;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _cc = GetComponent<CharacterController>();
        }

        void Start()
        {
            spawnPos = transform.position;
            PickWanderTarget();

            player = GameObject.FindWithTag("Player")?.transform;

            enemy = GetComponent<Geneforge.Gameplay.Characters.Enemies.Enemy>();
            if (enemy != null)
                enemy.OnDamaged += HandleOnDamaged;
        }

        void OnDestroy()
        {
            if (enemy != null)
                enemy.OnDamaged -= HandleOnDamaged;
        }

        void HandleOnDamaged(float dmg)
        {
            if (!isDamagePaused)
            {
                isDamagePaused = true;
                damagePauseTimer = 0f;
            }
        }

        void StartTranslationLock()
        {
            _translationLocked = true;
            var p = transform.position;
            _pinnedXZ = new Vector3(p.x, 0f, p.z);

            if (_rb)
            {
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }

                _rbPrevConstraints = _rb.constraints;
                _rbHadConstraints = true;
                _rb.constraints = _rbPrevConstraints
                                  | RigidbodyConstraints.FreezePositionX
                                  | RigidbodyConstraints.FreezePositionZ;

                if (hardStopFreezeRotationY)
                    _rb.constraints |= RigidbodyConstraints.FreezeRotationY;
            }

            ApplyRootMotionLock(true);
        }

        void EndTranslationLock()
        {
            _translationLocked = false;

            if (_rb && _rbHadConstraints)
            {
                _rb.constraints = _rbPrevConstraints;
                _rbHadConstraints = false;
            }

            ApplyRootMotionLock(false);
            ReleaseRotationFreeze();
        }

        void Update()
        {
            // Atualiza e limpa escudos expirados ANTES de qualquer return
            UpdateActiveShields();

            // MORTE → congela e agenda despawn ~5s (tempo real)
            if (enemy != null && enemy.CurrentHealth <= 0f)
            {
                if (!_deathFrozen)
                {
                    AnimatorSpeed(0f);
                    HardStopNow();
                    if (!_translationLocked) StartTranslationLock();
                    _deathFrozen = true;
                }
                if (!_despawnStarted)
                {
                    _despawnStarted = true;
                    StartCoroutine(DespawnAfterRealtime(5f));
                }
                return;
            }

            // Estados “ocupados”
            bool inAttackAnim = IsInAttackAnim();
            bool inHitAnim    = IsInHitAnim();
            bool inBusyAnim   = inAttackAnim || inHitAnim;

            // Lock de translação
            bool wantsTranslationLock = isDamagePaused || inBusyAnim;
            if (wantsTranslationLock && !_translationLocked) StartTranslationLock();
            else if (!wantsTranslationLock && _translationLocked) EndTranslationLock();

            // Pin de rotação
            bool shouldPinFacing =
                (inAttackAnim && freezeRotationWhileAttacking) ||
                (inHitAnim    && freezeRotationWhileHit);

            if (inBusyAnim && !_facingPinned && shouldPinFacing)
            {
                _facingPinned = true;
                _pinnedFacing = transform.rotation;
            }
            else if (!inBusyAnim && _facingPinned)
            {
                _facingPinned = false;
            }

            // Pausa de dano
            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: true);
                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
            }

            // Estado (Suporte ataca parado)
            float distToPlayer = (player != null) ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
            state = (distToPlayer <= attackRange) ? State.Attacking : State.Wandering;

            if (state == State.Wandering && !stayStationary)
            {
                WanderMove();
            }
            else
            {
                AnimatorSpeed(0f);
            }

            if (state == State.Attacking && rotateInPlace && player != null)
            {
                if (!_facingPinned)
                {
                    Vector3 face = player.position - transform.position; face.y = 0f;
                    if (face.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(face.normalized);
                }
            }

            // Reaplicar pins
            if (_translationLocked)
            {
                var p = transform.position;
                transform.position = new Vector3(_pinnedXZ.x, p.y, _pinnedXZ.z);
            }
            if (_facingPinned)
                transform.rotation = _pinnedFacing;

            // Ataque (ritmo de animação)
            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;
                float roll = Random.value;
                if (roll < 0.6f) animator?.SetTrigger("Attack");
                else if (roll < 0.85f) animator?.SetTrigger("AttackB");
                else animator?.SetTrigger("AttackC");
            }
        }

        IEnumerator DespawnAfterRealtime(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime; // não pára com pause
                yield return null;
            }
            Destroy(gameObject);
        }

        void FixedUpdate()
        {
            if (_translationLocked) PinXZ();
        }

        void LateUpdate()
        {
            if (_translationLocked) PinXZ();
        }

        void PinXZ()
        {
            var p = transform.position;
            transform.position = new Vector3(_pinnedXZ.x, p.y, _pinnedXZ.z);

            if (_rb && !_rb.isKinematic)
            {
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                _rb.angularVelocity = Vector3.zero;
            }
        }

        // -------- ESCUDO (evento de animação) ----------
        public void OnAttackHit()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, shieldRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);

            // candidatos: outros inimigos vivos
            List<Transform> candidates = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (!col) continue;
                var root = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
                if (root == this.transform) continue;

                var allyEnemy = root.GetComponentInParent<Geneforge.Gameplay.Characters.Enemies.Enemy>();
                if (allyEnemy == null) continue;
                if (allyEnemy.CurrentHealth <= 0f) continue;

                if (!candidates.Contains(allyEnemy.transform))
                    candidates.Add(allyEnemy.transform);
            }

            // mais perto primeiro
            candidates.Sort((a, b) =>
            {
                float da = (a.position - transform.position).sqrMagnitude;
                float db = (b.position - transform.position).sqrMagnitude;
                return da.CompareTo(db);
            });

            int applied = 0;
            foreach (var t in candidates)
            {
                if (ApplyShieldToTarget(t))
                {
                    applied++;
                    if (maxAlliesPerCast > 0 && applied >= maxAlliesPerCast) break;
                }
            }
        }

        bool ApplyShieldToTarget(Transform target)
        {
            if (!target) return false;

            ShieldData data;
            if (!activeShields.TryGetValue(target, out data))
            {
                data = new ShieldData { root = target };

                // 1) Guardar e desligar colliders (invulnerável a overlaps/hitboxes)
                data.colliders.Clear();
                data.prevEnabled.Clear();
                target.GetComponentsInChildren(true, data.colliders);
                foreach (var c in data.colliders)
                {
                    if (!c) { data.prevEnabled.Add(false); continue; }
                    data.prevEnabled.Add(c.enabled);
                    c.enabled = false;
                }

                // 2) Guardar e desativar CharacterController (se existir)
                data.cc = target.GetComponentInParent<CharacterController>();
                if (data.cc != null)
                {
                    data.prevCCEnabled = data.cc.enabled;
                    data.cc.enabled = false;
                }

                // 3) RB kinematic (não cai/é empurrado)
                data.rb = target.GetComponentInParent<Rigidbody>();
                if (data.rb != null)
                {
                    data.prevIsKinematic = data.rb.isKinematic;
                    data.rb.isKinematic  = true;
                    data.rb.linearVelocity = Vector3.zero;
                    data.rb.angularVelocity = Vector3.zero;
                }

                // 4) Trocar Layers (reforço)
                data.originalLayers.Clear();
                if (shieldedLayer >= 0)
                    RecordAndSetLayerRecursively(target, data.originalLayers, shieldedLayer);

                // 5) Trocar Tag do root (reforço contra filtros por tag)
                var go = target.gameObject;
                data.originalTag = go.tag;
                go.tag = "Untagged";

                // 6) Círculo visual
                data.circleObj = new GameObject("ShieldCircle");
                data.lr = data.circleObj.AddComponent<LineRenderer>();
                data.lr.useWorldSpace = true;
                data.lr.loop = true;
                data.lr.widthMultiplier = circleWidth;
                data.lr.material = new Material(Shader.Find("Sprites/Default"));
                data.lr.positionCount = circleSegments;

                activeShields.Add(target, data);
            }

            data.endTime = Time.time + shieldDuration;
            return true;
        }

        void UpdateActiveShields()
        {
            if (activeShields.Count == 0) return;

            var toRemove = new List<Transform>();

            foreach (var kv in activeShields)
            {
                var data = kv.Value;
                if (!data.root) { toRemove.Add(kv.Key); continue; }

                // atualizar círculo
                if (data.circleObj && data.lr)
                {
                    Vector3 center = data.root.position + indicatorOffset;
                    for (int i = 0; i < circleSegments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / circleSegments;
                        float x = Mathf.Cos(angle) * circleRadius;
                        float z = Mathf.Sin(angle) * circleRadius;
                        data.lr.SetPosition(i, center + new Vector3(x, 0f, z));
                    }
                }

                // terminou o escudo?
                if (Time.time >= data.endTime)
                {
                    // 1) Restaurar colliders
                    for (int i = 0; i < data.colliders.Count; i++)
                    {
                        var c = data.colliders[i];
                        if (!c) continue;
                        bool prev = (i < data.prevEnabled.Count) ? data.prevEnabled[i] : true;
                        c.enabled = prev;
                    }
                    data.colliders.Clear();
                    data.prevEnabled.Clear();

                    // 2) Restaurar CharacterController
                    if (data.cc != null) data.cc.enabled = data.prevCCEnabled;

                    // 3) Restaurar RB
                    if (data.rb != null) data.rb.isKinematic = data.prevIsKinematic;

                    // 4) Restaurar Layers
                    RestoreLayers(data.originalLayers);

                    // 5) Restaurar Tag
                    if (data.root) data.root.gameObject.tag = data.originalTag;

                    // 6) Remover círculo
                    if (data.circleObj) Destroy(data.circleObj);

                    toRemove.Add(kv.Key);
                }
            }

            foreach (var t in toRemove) activeShields.Remove(t);
        }

        // -------- Wander ----------
        void WanderMove()
        {
            Vector3 targetPos = wanderTarget;
            float speed = isIdleWaiting ? 0f : wanderSpeed;

            if (speed > 0f && !_translationLocked)
            {
                Vector3 dir = targetPos - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.position += dir.normalized * speed * Time.deltaTime;

                    if (!_facingPinned)
                        transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            AnimatorSpeed(Mathf.Clamp01(speed / Mathf.Max(0.01f, wanderSpeed)));

            if (!isIdleWaiting)
            {
                wanderTimer += Time.deltaTime;
                if (wanderTimer >= wanderInterval || Vector3.Distance(transform.position, wanderTarget) < 0.2f)
                {
                    isIdleWaiting = true; idleWaitTimer = 0f;
                }
            }
            else
            {
                idleWaitTimer += Time.deltaTime;
                if (idleWaitTimer >= idleWaitDuration)
                {
                    isIdleWaiting = false; wanderTimer = 0f; PickWanderTarget();
                }
            }
        }

        void PickWanderTarget()
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, shieldRadius);
            Gizmos.color = Color.red;   Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        // ----------------- Helpers -----------------

        bool IsInAttackAnim()
        {
            if (animator == null) return false;
            var st = animator.GetCurrentAnimatorStateInfo(0);

            foreach (var n in attackStateNames)
                if (!string.IsNullOrEmpty(n) && st.IsName(n))
                    return true;

            foreach (var t in attackStateTags)
                if (!string.IsNullOrEmpty(t) && st.IsTag(t))
                    return true;

            return false;
        }

        bool IsInHitAnim()
        {
            if (animator == null) return false;
            var st = animator.GetCurrentAnimatorStateInfo(0);

            foreach (var n in hitStateNames)
                if (!string.IsNullOrEmpty(n) && st.IsName(n))
                    return true;

            foreach (var t in hitStateTags)
                if (!string.IsNullOrEmpty(t) && st.IsTag(t))
                    return true;

            return false;
        }

        void AnimatorSpeed(float v)
        {
            if (animator != null) animator.SetFloat("Speed", v);
        }

        void ApplyRootMotionLock(bool locked)
        {
            if (animator == null || !disableRootMotionWhenLocked) return;
            animator.applyRootMotion = !locked;
        }

        void HardStopNow(bool alsoFreezeRotationY = true)
        {
            if (animator)
            {
                animator.applyRootMotion = false;
                animator.SetFloat("Speed", 0f);
            }

            if (hardStopRigidbodyWhenLocked && _rb)
            {
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }

                if (alsoFreezeRotationY && hardStopFreezeRotationY)
                    _rb.constraints |= RigidbodyConstraints.FreezeRotationY;
            }
        }

        void ReleaseRotationFreeze()
        {
            if (_rb && hardStopFreezeRotationY)
                _rb.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        }

        // ===== Layers: registar/trocar/restaurar =====
        void RecordAndSetLayerRecursively(Transform root, Dictionary<Transform,int> store, int newLayer)
        {
            if (!root) return;

            if (!store.ContainsKey(root))
                store.Add(root, root.gameObject.layer);

            if (newLayer >= 0)
                root.gameObject.layer = newLayer;

            for (int i = 0; i < root.childCount; i++)
                RecordAndSetLayerRecursively(root.GetChild(i), store, newLayer);
        }

        void RestoreLayers(Dictionary<Transform,int> store)
        {
            if (store == null) return;
            foreach (var kv in store)
            {
                var t = kv.Key;
                if (t) t.gameObject.layer = kv.Value;
            }
            store.Clear();
        }
    }
}
