using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies; // necessário para Enemy

namespace Geneforge.Gameplay.Characters.Enemies.Ranged
{
    [RequireComponent(typeof(Enemy))]
    public class PoisonKid : MonoBehaviour
    {
        [Header("Referências")]
        public Animator animator;

        [Header("Comportamento")]
        public bool stayStationary = false;
        public bool rotateInPlace = true;

        [Header("Wander")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;
        public float idleWaitDuration = 1f;

        [Header("Perceção / Ataque")]
        public float detectionRadius = 20f;
        public float chaseSpeed = 4f;
        public float attackRange = 12f;
        public float attackRate = 1.25f;

        [Header("Direção do lançamento")]
        public bool requireLineOfSight = true;
        public LayerMask lineOfSightMask = ~0;
        public float lineOfSightPadding = 0.1f;

        [Header("Bola de Veneno (Projectile)")]
        public GameObject poisonBallPrefab;
        public Transform throwOrigin;
        public float throwForce = 30f;
        public Vector3 aimOffset = Vector3.zero;

        [Header("Fallback - Soundwave (quando não há PoisonBall)")]
        public SoundwaveAttack soundwaveAttack;   // adicionar no mesmo GO e arrastar aqui
        public bool useSoundwaveIfNoPoisonBall = true;
        public Vector3 soundwaveCenterOffset = Vector3.zero;

        [Header("Pausa ao sofrer dano")]
        public float damagePauseDuration = 0.5f;

        // ------- Motion Lock -------
        [Header("Motion Control (Lock de Movimento)")]
        public bool disableRootMotionWhenLocked = true;
        public bool hardStopRigidbodyWhenLocked = true;
        public bool hardStopFreezeRotationY = true;

        [Header("Attack Lock / Detection")]
        public bool freezeRotationWhileAttacking = true;
        public string[] attackStateNames = { "Attack" };
        public string[] attackStateTags = { "Attack" };

        [Header("Hit Lock / Detection")]
        public bool freezeRotationWhileHit = true;
        public string[] hitStateNames = { "Hit", "Hurt" };
        public string[] hitStateTags = { "Hurt" };

        [Header("Animator – Death State")]
        public string deathStateName = "Death";

        // ---- estado interno ----
        Vector3 spawnPos, wanderTarget;
        float wanderTimer, lastAttackTime;
        enum State { Wandering, Chasing, Attacking }
        State state = State.Wandering;

        Transform player;
        float currentSpeed;
        bool isIdleWaiting;
        float idleWaitTimer;

        bool isDamagePaused;
        float damagePauseTimer;

        Collider[] ownerCols;

        Enemy enemy;
        Rigidbody _rb;

        bool _translationLocked;
        Vector3 _pinnedXZ;
        RigidbodyConstraints _rbPrevConstraints;

        bool _deadLatched;
        int _deathHash;

        bool _attackFacingPinned;
        Quaternion _attackFacing;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            enemy = GetComponent<Enemy>();

            if (!string.IsNullOrEmpty(deathStateName))
                _deathHash = Animator.StringToHash(deathStateName);
        }

        void Start()
        {
            spawnPos = transform.position;
            PickWanderTarget();

            player = GameObject.FindWithTag("Player")?.transform;

            if (enemy)
                enemy.OnDamaged += HandleOnDamaged;

            ownerCols = GetComponentsInChildren<Collider>(true);

            // fallback auto se não estiver ligado no Inspector
            if (!soundwaveAttack)
                soundwaveAttack = GetComponent<SoundwaveAttack>();
        }

        void OnDestroy()
        {
            if (enemy)
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

        void Update()
        {
            if (!_deadLatched && enemy && enemy.CurrentHealth <= 0f)
                LatchDeathStop();

            if (_deadLatched) { PinXZ(); return; }

            bool inAttackAnim = IsInAttackAnim();
            bool inHitAnim = IsInHitAnim();
            bool inBusyAnim = inAttackAnim || inHitAnim;

            bool wantsLock = isDamagePaused || inBusyAnim;
            if (wantsLock && !_translationLocked) StartTranslationLock();
            else if (!wantsLock && _translationLocked) EndTranslationLock();

            bool shouldPinFacing =
                (inAttackAnim && freezeRotationWhileAttacking) ||
                (inHitAnim && freezeRotationWhileHit);

            if (inBusyAnim && !_attackFacingPinned && shouldPinFacing)
            {
                _attackFacingPinned = true;
                _attackFacing = transform.rotation;
            }
            else if (!inBusyAnim && _attackFacingPinned)
                _attackFacingPinned = false;

            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                AnimatorSpeed(0f);
                HardStopNow(true);

                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
            }

            if (inHitAnim)
            {
                AnimatorSpeed(0f);
                HardStopNow(true);
                return;
            }

            if (!player) return;

            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= attackRange && (!requireLineOfSight || HasLineOfSight()))
                state = State.Attacking;
            else if (!stayStationary && dist <= detectionRadius)
                state = State.Chasing;
            else
                state = State.Wandering;

            float targetSpeed;
            Vector3 targetPos;

            switch (state)
            {
                case State.Chasing:
                    targetPos = player.position;
                    targetSpeed = chaseSpeed;
                    break;

                case State.Wandering:
                    targetPos = wanderTarget;
                    targetSpeed = isIdleWaiting ? 0f : wanderSpeed;
                    break;

                default:
                    targetPos = transform.position;
                    targetSpeed = 0f;
                    break;
            }

            currentSpeed = targetSpeed;

            if (currentSpeed > 0f)
            {
                Vector3 dir = targetPos - transform.position;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.1f)
                {
                    if (!_translationLocked)
                        transform.position += dir.normalized * currentSpeed * Time.deltaTime;

                    if (!_attackFacingPinned)
                        transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }
            else HardStopNow(false);

            if ((state == State.Attacking || rotateInPlace) && !_attackFacingPinned)
            {
                Vector3 face = player.position - transform.position;
                face.y = 0f;

                if (face.sqrMagnitude > 0.1f)
                    transform.rotation = Quaternion.LookRotation(face.normalized);
            }

            if (_attackFacingPinned)
                transform.rotation = _attackFacing;

            if (animator)
            {
                float ns = chaseSpeed > 0 ? Mathf.Clamp01(currentSpeed / chaseSpeed) : 0f;
                AnimatorSpeed(ns);
            }

            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;
                if (animator) animator.SetTrigger("Attack");
            }

            // Wander
            if (state == State.Wandering)
            {
                if (!isIdleWaiting)
                {
                    wanderTimer += Time.deltaTime;
                    if (wanderTimer >= wanderInterval ||
                        Vector3.Distance(transform.position, wanderTarget) < 0.3f)
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

        void FixedUpdate() { if (_translationLocked || _deadLatched) PinXZ(); }
        void LateUpdate() { if (_translationLocked || _deadLatched) PinXZ(); }

        // ---------------- ATAQUE ----------------
        public void OnThrowRelease() => ThrowPoisonBall();

        void ThrowPoisonBall()
        {
            if (!player) return;

            // ---------------------------------------------------
            // FALLBACK: se não existe prefab, dispara Soundwave
            // (sem prefab, animação tipo Nautilus)
            // ---------------------------------------------------
            if (poisonBallPrefab == null)
            {
                if (useSoundwaveIfNoPoisonBall && soundwaveAttack != null)
                {
                    Vector3 center = transform.position + soundwaveCenterOffset;
                    soundwaveAttack.Fire(center);
                }
                else
                {
                    Debug.LogWarning("[PoisonKid] poisonBallPrefab é null e soundwaveAttack não está atribuído.");
                }
                return;
            }

            // ---------------------------------------------------
            // PoisonBall normal
            // ---------------------------------------------------
            Vector3 origin = throwOrigin ? throwOrigin.position : transform.position;
            GameObject ball = Instantiate(poisonBallPrefab, origin, Quaternion.identity);

            Vector3 targetPos = player.position + aimOffset;
            Vector3 dir = (targetPos - origin);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            dir.Normalize();

            if (ownerCols == null || ownerCols.Length == 0)
                ownerCols = GetComponentsInChildren<Collider>(true);

            var proj = ball.GetComponent<PoisonBallProjectile>();
            if (proj)
                proj.Launch(dir * throwForce, ownerCols, transform, attackRange);
        }

        // ---------------- Auxiliares ----------------

        bool HasLineOfSight()
        {
            if (!player) return false;

            Vector3 origin = throwOrigin ? throwOrigin.position : transform.position;
            Vector3 target = player.position + aimOffset;

            Vector3 diff = target - origin;
            float dist = diff.magnitude - lineOfSightPadding;
            if (dist <= 0.1f) return true;

            Vector3 dir = diff.normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform != player && !hit.collider.transform.IsChildOf(player))
                    return false;
            }
            return true;
        }

        bool IsInAttackAnim()
        {
            if (!animator) return false;
            var st = animator.GetCurrentAnimatorStateInfo(0);

            foreach (string n in attackStateNames)
                if (st.IsName(n)) return true;

            foreach (string tag in attackStateTags)
                if (st.IsTag(tag)) return true;

            return false;
        }

        bool IsInHitAnim()
        {
            if (!animator) return false;
            var st = animator.GetCurrentAnimatorStateInfo(0);

            foreach (string n in hitStateNames)
                if (st.IsName(n)) return true;

            foreach (string tag in hitStateTags)
                if (st.IsTag(tag)) return true;

            return false;
        }

        void AnimatorSpeed(float v)
        {
            if (animator) animator.SetFloat("Speed", v);
        }

        void PickWanderTarget()
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void PinXZ()
        {
            var p = transform.position;
            transform.position = new Vector3(_pinnedXZ.x, p.y, _pinnedXZ.z);

            if (_rb)
                _rb.linearVelocity = Vector3.zero;
        }

        void StartTranslationLock()
        {
            _translationLocked = true;

            var p = transform.position;
            _pinnedXZ = new Vector3(p.x, 0f, p.z);

            if (_rb)
            {
                _rbPrevConstraints = _rb.constraints;
                _rb.constraints |= RigidbodyConstraints.FreezePositionX;
                _rb.constraints |= RigidbodyConstraints.FreezePositionZ;
            }
        }

        void EndTranslationLock()
        {
            _translationLocked = false;

            if (_rb)
                _rb.constraints = _rbPrevConstraints;
        }

        void HardStopNow(bool freezeY)
        {
            if (_rb)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;

                if (freezeY)
                    _rb.constraints |= RigidbodyConstraints.FreezeRotationY;
            }
        }

        void LatchDeathStop()
        {
            _deadLatched = true;

            if (animator)
            {
                animator.SetFloat("Speed", 0f);
                animator.applyRootMotion = false;
            }

            HardStopNow(true);
            StartTranslationLock();
        }
    }
}
