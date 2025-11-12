// Melee.cs
using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Melee
{
    public class Melee : MonoBehaviour
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
        public string[] hitStateNames = { "Damaged", "Hit", "Hurt" }; // ajusta aos teus nomes
        public string[] hitStateTags  = { "Hurt", "Damaged" };
        // ------------------------------------

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

        // Damage pause
        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        // Enemy ref (para morte/despawn)
        Geneforge.Gameplay.Characters.Enemies.Enemy enemy;

        // --- Lock de Translação (anti-deslize) ---
        bool _translationLocked = false;
        Vector3 _pinnedXZ; // XZ ancorados durante lock
        Rigidbody _rb;
        CharacterController _cc;
        RigidbodyConstraints _rbPrevConstraints;
        bool _rbHadConstraints = false;

        // --- Pin de rotação durante ataque/hit ---
        bool _attackFacingPinned = false;
        Quaternion _attackFacing;

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
            // --- MORTE: parar já; Enemy destrói aos ~5s ---
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

            // -------- Lock de Translação (sem returns) ----------
            // Lock durante: dano (timer) OU enquanto o clip de hit/ataque está ativo
            bool wantsTranslationLock = isDamagePaused || inBusyAnim;

            if (wantsTranslationLock && !_translationLocked) StartTranslationLock();
            else if (!wantsTranslationLock && _translationLocked) EndTranslationLock();
            // ----------------------------------------------------

            // --- Pin de rotação enquanto a animação "ocupada" decorre ---
            bool shouldPinFacing =
                (inAttackAnim && freezeRotationWhileAttacking) ||
                (inHitAnim    && freezeRotationWhileHit);

            if (inBusyAnim && !_attackFacingPinned && shouldPinFacing)
            {
                _attackFacingPinned = true;
                _attackFacing = transform.rotation;
            }
            else if (!inBusyAnim && _attackFacingPinned)
            {
                _attackFacingPinned = false;
            }

            // Dano: avançar timer (não fazemos return — mantém lógica viva)
            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: true);

                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
            }

            // Invisibilidade → como se o player não existisse
            if (A_ChameleonCamouflage.InvisibleActive)
            {
                state = State.Wandering;
                currentSpeed = 0f;
                AnimatorSpeed(0f);
                HardStopNow(alsoFreezeRotationY: false);
                return;
            }

            if (player == null) return;

            // Estado
            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= attackRange) state = State.Attacking;
            else if (!stayStationary && dist <= detectionRadius) state = State.Chasing;
            else state = State.Wandering;

            // Movimento
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
                if (dir.sqrMagnitude > 0.01f)
                {
                    if (!_translationLocked)
                        transform.position += dir.normalized * currentSpeed * Time.deltaTime;

                    // Não rodar se a rotação estiver pinada (ataque ou hit)
                    if (!_attackFacingPinned)
                        transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }
            else
            {
                HardStopNow(alsoFreezeRotationY: false);
            }

            // Rodar em ataque (tracking), mas não durante o pin
            if (state == State.Attacking && rotateInPlace && player != null)
            {
                if (!_attackFacingPinned)
                {
                    Vector3 face = player.position - transform.position;
                    face.y = 0f;
                    if (face.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(face.normalized);
                }
            }

            // Reaplica rotação pinada (garante que nada a altera)
            if (_attackFacingPinned)
                transform.rotation = _attackFacing;

            // Animação
            float normSpeed = chaseSpeed > 0f ? Mathf.Clamp01(currentSpeed / chaseSpeed) : 0f;
            AnimatorSpeed(normSpeed);

            // Ataque (timing)
            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;

                float roll = Random.value;          // 0..1
                if (roll < 0.6f) animator.SetTrigger("Attack");
                else if (roll < 0.85f) animator.SetTrigger("AttackB");
                else animator.SetTrigger("AttackC");
            }
            // Wander
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

        // Re-pin em física e em late (cobre drift pós-Update)
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

        public void OnAttackHit()
        {
            if (playerHealth != null)
                playerHealth.ApplyDamage(damagePerHit);

            if (!applyKnockback || player == null) return;

            // Knockback no PLAYER (não no inimigo)
            Vector3 dir = (player.position - transform.position);
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
                Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, wanderRadius);
                Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }
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
    }
}
