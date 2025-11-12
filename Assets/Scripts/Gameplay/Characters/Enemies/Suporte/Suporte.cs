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
        public float attackRate = 1f; // ritmo das animações Attack/AttackB/AttackC

        [Header("Escudo")]
        public float shieldDuration = 5f;
        public float shieldRadius = 8f;
        public int maxAlliesPerCast = 0;

        [Header("Visual do Círculo")]
        public Vector3 indicatorOffset = new Vector3(0f, 2.0f, 0f);
        public float circleRadius = 0.5f;
        public int circleSegments = 24;
        public float circleWidth = 0.05f;

        [Header("Layer (Opcional)")]
        public int shieldedLayer = -1;

        [Header("Feedback a Dano")]
        public float damagePauseDuration = 0.5f;

        // ------- Lock / Motion Control (anti-deslize) -------
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
        // -----------------------------------------------------

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

        // ---- Refs auxiliares / anti-deslize ----
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

        // ----- Dados do Escudo -----
        class ShieldData
        {
            public Transform target;
            public float endTime;
            public int originalLayer = -1;

            public Rigidbody rb;          // RB do alvo (se existir)
            public bool prevIsKinematic;  // estado anterior
            public GameObject circleObj;
            public LineRenderer lr;
        }

        readonly Dictionary<Transform, ShieldData> activeShields = new Dictionary<Transform, ShieldData>();

        // ----- Congelar ao morrer (alinha com Animal) -----
        bool _deathFrozen = false;

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
            UpdateActiveShields();

            // MORTE → congelar de vez e deixar o Enemy tratar do despawn (~5s)
            if (enemy != null && enemy.CurrentHealth <= 0f)
            {
                if (!_deathFrozen)
                {
                    AnimatorSpeed(0f);
                    HardStopNow();
                    if (!_translationLocked) StartTranslationLock();
                    _deathFrozen = true;

                    // Desativar este "brain" para não voltar a mexer nem gastar CPU
                    enabled = false;
                }
                return;
            }

            // Estados “ocupados”
            bool inAttackAnim = IsInAttackAnim();
            bool inHitAnim    = IsInHitAnim();
            bool inBusyAnim   = inAttackAnim || inHitAnim;

            // Lock de translação enquanto está em hit/ataque, ou durante o timer de dano
            bool wantsTranslationLock = isDamagePaused || inBusyAnim;

            if (wantsTranslationLock && !_translationLocked) StartTranslationLock();
            else if (!wantsTranslationLock && _translationLocked) EndTranslationLock();

            // Pin da rotação (não vira a meio do ataque/hit)
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

            // Dano (pausa, mas sem dar return — para manter pin ativo)
            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: true);
                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
            }

            // Estado (Suporte ataca parado — usa range)
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

        // -------- ESCUDO: chamado via evento da animação ----------
        public void OnAttackHit()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, shieldRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);

            List<Transform> candidates = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (!col) continue;
                var root = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
                if (root == this.transform) continue;

                var allyEnemy = root.GetComponentInParent<Geneforge.Gameplay.Characters.Enemies.Enemy>();
                if (allyEnemy == null) continue;

                if (!candidates.Contains(allyEnemy.transform))
                    candidates.Add(allyEnemy.transform);
            }

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
                data = new ShieldData { target = target };

                // Guardar RB e tornar kinematic durante o escudo (em vez de desligar colliders)
                data.rb = target.GetComponentInParent<Rigidbody>();
                if (data.rb != null)
                {
                    data.prevIsKinematic = data.rb.isKinematic;
                    data.rb.isKinematic  = true; // não reage à física, não "cai"
                }

                // Mudar layer se configurado
                if (shieldedLayer >= 0)
                {
                    data.originalLayer = target.gameObject.layer;
                    SetLayerRecursively(target.gameObject, shieldedLayer);
                }

                // Círculo visual
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
                if (!data.target) { toRemove.Add(kv.Key); continue; }

                // atualizar posição e círculo
                if (data.circleObj && data.lr)
                {
                    Vector3 center = data.target.position + indicatorOffset;
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
                    // Restaurar RB
                    if (data.rb != null)
                        data.rb.isKinematic = data.prevIsKinematic;

                    // Restaurar layer
                    if (data.originalLayer >= 0)
                        SetLayerRecursively(data.target.gameObject, data.originalLayer);

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

            if (speed > 0f && !_translationLocked) // não mover se está bloqueado
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
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
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
            // CharacterController: basta não chamar Move
        }

        void ReleaseRotationFreeze()
        {
            if (_rb && hardStopFreezeRotationY)
                _rb.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        }

        static void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (!obj) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
                if (child) SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
