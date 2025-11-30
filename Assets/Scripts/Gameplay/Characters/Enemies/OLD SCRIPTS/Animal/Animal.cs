using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Animal
{
    public class Animal : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public PlayerHealth playerHealth;

        [Header("Behavior")]
        public bool stayStationary = false;
        public bool rotateInPlace = true;

        [Header("Wander Settings")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;
        public float idleWaitDuration = 1f;

        [Header("Chase & Attack")]
        public float detectionRadius = 20f;
        public float chaseSpeed = 4f;
        public float attackRange = 1.5f;
        public float attackRate = 1f;
        public float damagePerHit = 10f;

        [Header("Knockback Target (Optional)")]
        public Transform knockbackRootOverride;

        [Header("Knockback")]
        public bool applyKnockback = true;
        public float knockbackForce = 6f;
        public float knockbackUpward = 0.5f;
        public float knockbackMaxSpeed = 12f;

        [Header("Damage Pause")]
        public float damagePauseDuration = 0.5f;

        // -------- Attack windows / Hitboxes --------
        [Header("Attack Windows / Hitboxes")]
        public Transform bitePoint;
        public Transform tailPoint;
        public float biteRadius = 0.9f;
        public float tailRadius = 1.1f;
        public LayerMask playerMask;

        [Header("Attack Lock")]
        public bool lockDuringAttack = true;
        public float attackLockExtraTime = 0.05f;
        public bool freezeRotationWhileAttacking = true;

        // -------- Hard Lock / Motion Control --------
        [Header("Hard Lock")]
        public bool lockOnDamage = true;
        public bool lockOnAttack = true;
        public string[] attackStateNames = { "Attack", "AttackB" };
        public string[] attackStateTags  = { "Attack" };

        [Header("Motion Control")]
        public bool disableRootMotionWhenLocked = true;
        public bool hardStopRigidbodyWhenLocked = true;
        public bool hardStopFreezeRotationY = true;

        // -------- Hit Lock / Detection (NOVO) --------
        [Header("Hit Lock / Detection")]
        public bool freezeRotationWhileHit = true;
        public string[] hitStateNames = { "Damaged", "Hit", "Hurt" };
        public string[] hitStateTags  = { "Hurt", "Damaged" };
        // ---------------------------------------------

        bool isAttackLocked = false;
        Coroutine attackLockCo;
        Collider[] _hitBuf = new Collider[4];

        Vector3 spawnPos;
        Vector3 wanderTarget;
        float wanderTimer;
        float lastAttackTime;

        enum State { Wandering, Chasing, Attacking }
        State state = State.Wandering;

        Transform player;
        float currentSpeed;
        bool isIdleWaiting = false;
        float idleWaitTimer = 0f;

        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        Geneforge.Gameplay.Characters.Enemies.Enemy enemy;
        Rigidbody _rb;
        CharacterController _cc;

        // --- Lock de Translação (evita deslize) ---
        bool _translationLocked = false;
        Vector3 _pinnedXZ;
        RigidbodyConstraints _rbPrevConstraints;
        bool _rbHadConstraints = false;

        // --- Pin de rotação durante ataque/hit (opcional mas útil) ---
        bool _facingPinned = false;
        Quaternion _pinnedFacing;

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
            if (player != null && playerHealth == null)
                playerHealth = player.GetComponent<PlayerHealth>();

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
            // MORTE: parar já. Enemy trata do despawn ~5s depois.
            if (enemy != null && enemy.CurrentHealth <= 0f)
            {
                AnimatorSpeed(0f);
                HardStopNow();
                if (!_translationLocked) StartTranslationLock(); // fica pregado até desaparecer
                return;
            }

            // Estados de animação "ocupados"
            bool inAttackAnim = IsInAttackAnim();
            bool inHitAnim    = IsInHitAnim();
            bool inBusyAnim   = inAttackAnim || inHitAnim;

            // Lock de translação se: dano (timer) OU enquanto clip de hit/ataque decorre
            bool wantsTranslationLock = (lockOnDamage && isDamagePaused) || inBusyAnim;

            if (wantsTranslationLock && !_translationLocked) StartTranslationLock();
            else if (!wantsTranslationLock && _translationLocked) EndTranslationLock();

            // Pin de rotação durante ataque/hit (para não virar a meio)
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

            // Dano → pausa
            if (lockOnDamage && isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: true);

                if (damagePauseTimer >= damagePauseDuration)
                {
                    isDamagePaused = false;
                }
            }

            // Invisibilidade
            if (A_ChameleonCamouflage.InvisibleActive)
            {
                state = State.Wandering;
                currentSpeed = 0f;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: false);
                return;
            }

            if (player == null) return;

            // fallback lock de ataque (timer)
            if (isAttackLocked)
            {
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: freezeRotationWhileAttacking);
                if (!_translationLocked) StartTranslationLock();
            }

            // estado
            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= attackRange)
                state = State.Attacking;
            else if (!stayStationary && dist <= detectionRadius)
                state = State.Chasing;
            else
                state = State.Wandering;

            // movimento/rotação
            float targetSpeed;
            Vector3 targetPos;

            switch (state)
            {
                case State.Chasing:
                    if (stayStationary) { targetPos = transform.position; targetSpeed = 0f; }
                    else { targetPos = player.position; targetSpeed = chaseSpeed; }
                    break;

                case State.Wandering:
                    if (stayStationary) { targetPos = transform.position; targetSpeed = 0f; }
                    else { targetPos = wanderTarget; targetSpeed = isIdleWaiting ? 0f : wanderSpeed; }
                    break;

                default: // Attacking
                    targetPos = transform.position;
                    targetSpeed = 0f;
                    break;
            }

            currentSpeed = targetSpeed;

            if (currentSpeed > 0f)
            {
                Vector3 dir = targetPos - transform.position;
                dir.y = 0f;

                if (state == State.Chasing && player != null)
                {
                    float d = Vector3.Distance(transform.position, player.position);
                    float stopDist = Mathf.Max(attackRange * 0.95f, attackRange - 0.1f);
                    if (d <= stopDist) currentSpeed = 0f;
                }

                if (dir.sqrMagnitude > 0.01f && currentSpeed > 0f)
                {
                    if (!_translationLocked)
                        transform.position += dir.normalized * currentSpeed * Time.deltaTime;

                    if (!_facingPinned)
                        transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }
            else
            {
                HardStopNow(alsoFreezeRotationY: false);
            }

            if (state == State.Attacking && rotateInPlace && player != null && !_facingPinned)
            {
                Vector3 face = player.position - transform.position;
                face.y = 0f;
                if (face.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(face.normalized);
            }

            // Ancorar XZ durante lock (garante zero deslize)
            if (_translationLocked)
            {
                var p = transform.position;
                transform.position = new Vector3(_pinnedXZ.x, p.y, _pinnedXZ.z);
            }

            // Reaplicar rotação pinada (se ativa)
            if (_facingPinned)
                transform.rotation = _pinnedFacing;

            float normSpeed = chaseSpeed > 0f ? Mathf.Clamp01(currentSpeed / chaseSpeed) : 0f;
            AnimatorSpeed(normSpeed);

            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;

                if (Random.value < 0.5f) animator.SetTrigger("Attack");
                else animator.SetTrigger("AttackB");

                if (lockDuringAttack && attackLockCo == null)
                    attackLockCo = StartCoroutine(AttackLockForCurrentState());
            }

            // wander
            if (!stayStationary && state == State.Wandering)
            {
                if (!isIdleWaiting)
                {
                    wanderTimer += Time.deltaTime;
                    if (wanderTimer >= wanderInterval ||
                        Vector3.Distance(transform.position, wanderTarget) < 0.2f)
                    {
                        isIdleWaiting = true;
                        idleWaitTimer = 0f;
                    }
                }
                else
                {
                    idleWaitTimer += Time.deltaTime;
                    if (idleWaitTimer >= idleWaitDuration)
                    {
                        isIdleWaiting = false;
                        wanderTimer = 0f;
                        PickWanderTarget();
                    }
                }
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

        // ---------- KNOCKBACK / DANO (Animation Events chamam estes) ----------
        public void AE_BiteHit()
        {
            if (bitePoint == null) return;
            int n = Physics.OverlapSphereNonAlloc(bitePoint.position, biteRadius, _hitBuf, playerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var ph = _hitBuf[i].GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    ph.ApplyDamage(damagePerHit);
                    ApplyKnockbackFrom(bitePoint.position);
                    break;
                }
            }
        }

        public void AE_TailHit()
        {
            if (tailPoint == null) return;
            int n = Physics.OverlapSphereNonAlloc(tailPoint.position, tailRadius, _hitBuf, playerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var ph = _hitBuf[i].GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    ph.ApplyDamage(damagePerHit);
                    ApplyKnockbackFrom(tailPoint.position);
                    break;
                }
            }
        }

        void ApplyKnockbackFrom(Vector3 fromPos)
        {
            if (!applyKnockback || player == null) return;

            Vector3 dir = (player.position - fromPos);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            Vector3 impulse = dir * knockbackForce + Vector3.up * knockbackUpward;
            Transform root = knockbackRootOverride != null ? knockbackRootOverride : player;

            var receiver =
                root.GetComponentInChildren<KnockbackReceiver>() ??
                root.GetComponentInParent<KnockbackReceiver>();

            if (receiver != null) { receiver.ApplyImpulse(impulse); return; }

            var rb =
                root.GetComponentInChildren<Rigidbody>() ??
                root.GetComponentInParent<Rigidbody>();

            if (rb != null)
            {
                rb.AddForce(impulse, ForceMode.Impulse);
                if (knockbackMaxSpeed > 0f)
                {
                    Vector3 v = rb.linearVelocity;
                    Vector3 horiz = new Vector3(v.x, 0f, v.z);
                    if (horiz.magnitude > knockbackMaxSpeed)
                    {
                        horiz = horiz.normalized * knockbackMaxSpeed;
                        rb.linearVelocity = new Vector3(horiz.x, v.y, horiz.z);
                    }
                }
                return;
            }

            var cc =
                root.GetComponentInChildren<CharacterController>() ??
                root.GetComponentInParent<CharacterController>();

            if (cc != null && cc.enabled)
            {
                StopCoroutine(nameof(CCKnockbackRoutine));
                StartCoroutine(CCKnockbackRoutine(cc, impulse, 0.25f, 8f));
            }
        }
        // ---------------------------------------------------------------------

        IEnumerator AttackLockForCurrentState()
        {
            isAttackLocked = true;

            float clipLen = 0.6f;
            if (animator != null)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.length > 0.01f) clipLen = st.length;
            }

            yield return new WaitForSeconds(clipLen + attackLockExtraTime);
            isAttackLocked = false;
            attackLockCo = null;
            EndTranslationLock(); // libertar translação quando termina a janela de ataque
        }

        IEnumerator CCKnockbackRoutine(CharacterController cc, Vector3 impulse, float duration, float decayRate)
        {
            float t = 0f;
            Vector3 vel = impulse;

            while (t < duration && cc != null && cc.enabled)
            {
                cc.Move(vel * Time.deltaTime);
                float k = Mathf.Clamp01(decayRate * Time.deltaTime);
                vel = Vector3.Lerp(vel, Vector3.zero, k);

                t += Time.deltaTime;
                yield return null;
            }
        }

        void PickWanderTarget()
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void OnDrawGizmosSelected()
        {
            if (!stayStationary)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(transform.position, wanderRadius);
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = Color.yellow;
            if (bitePoint) Gizmos.DrawWireSphere(bitePoint.position, biteRadius);
            Gizmos.color = Color.cyan;
            if (tailPoint) Gizmos.DrawWireSphere(tailPoint.position, tailRadius);
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

        // (Opcional) Eventos de animação
        public void AE_AttackLockOn()  { HardStopNow(); }
        public void AE_AttackLockOff() { ReleaseRotationFreeze(); }
    }
}
