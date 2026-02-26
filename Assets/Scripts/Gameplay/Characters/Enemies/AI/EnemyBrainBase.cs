using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    /// <summary>
    /// Base class for all enemy brains. Owns high-level behaviour and locomotion,
    /// but never health/knockback/death (those live on EnemyCore).
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class EnemyBrainBase : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] protected EnemyCore enemy;
        [SerializeField] protected Animator animator;
        [SerializeField] protected Transform target;

        [Header("Movement")]
        [Tooltip("Used only to normalize Animator Speed; actual per-state speed is configured in child brains.")]
        [SerializeField] protected float defaultMoveSpeed = 3f;
        [SerializeField] protected float rotationSpeedDegPerSec = 540f;
        [SerializeField] protected bool faceTargetWhileMoving = true;

        [Header("Line of Sight (optional)")]
        [SerializeField] protected bool useLineOfSight = false;
        [SerializeField] protected LayerMask lineOfSightMask = ~0;
        [SerializeField] protected float lineOfSightPadding = 0.1f;

        [Header("Professional Combat")]
        [Tooltip("If true, this enemy won't be staggered/interrupted by damage animations (useful for bosses).")]
        [SerializeField] protected bool hasHyperArmor = false;

        public bool HasHyperArmor => hasHyperArmor;

        protected PlayerHealth playerHealth;

        protected Vector3 spawnPosition;
        bool spawnPositionInitialized;

        protected bool isDead;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int HitTagHash = Animator.StringToHash("Hit");
        private static readonly int AttackTagHash = Animator.StringToHash("Attack");
        
        // Cache common state names
        private static readonly int HitStateHash = Animator.StringToHash("Hit");
        private static readonly int DamagedStateHash = Animator.StringToHash("Damaged");
        private static readonly int AttackStateHash = Animator.StringToHash("Attack");
        private static readonly int Attack2StateHash = Animator.StringToHash("Attack2");
        private static readonly int Attack3StateHash = Animator.StringToHash("Attack3");
        private static readonly int AttackBStateHash = Animator.StringToHash("AttackB");
        private static readonly int AttackCStateHash = Animator.StringToHash("AttackC");
        private static readonly int EnrageStateHash = Animator.StringToHash("Enrage");

        public float DefaultMoveSpeed
        {
            get => defaultMoveSpeed;
            set => defaultMoveSpeed = value;
        }

        protected virtual void Reset()
        {
            enemy = GetComponent<EnemyCore>();
            animator = GetComponentInChildren<Animator>();
        }

        protected virtual void Awake()
        {
            if (enemy == null)
                enemy = GetComponent<EnemyCore>();

            if (animator == null)
            {
                if (enemy != null && enemy.Animator != null)
                    animator = enemy.Animator;
                else
                    animator = GetComponentInChildren<Animator>();
            }


            if (animator != null)
                animator.applyRootMotion = false;

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
                playerHealth = playerObj.GetComponent<PlayerHealth>();
            }

            if (!spawnPositionInitialized)
            {
                spawnPosition = transform.position;
                spawnPositionInitialized = true;
            }
        }

        protected virtual void OnEnable()
        {
            isDead = false;
            if (enemy != null)
            {
                enemy.OnDamaged += HandleDamaged;
                enemy.OnStaggered += HandleStaggered;
                enemy.OnFirstHit += HandleFirstHit;
                enemy.OnDied += HandleOwnerDied;
            }
        }

        protected virtual void OnDisable()
        {
            if (enemy != null)
            {
                enemy.OnDamaged -= HandleDamaged;
                enemy.OnStaggered -= HandleStaggered;
                enemy.OnFirstHit -= HandleFirstHit;
                enemy.OnDied -= HandleOwnerDied;
            }
        }

        protected virtual void Update()
        {
            if (isDead || enemy == null || enemy.IsDead) return;

            // 👉 LOCK ANIMATION CHECK
            if (animator != null)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                
                bool isLocked = state.tagHash == HitTagHash || state.shortNameHash == HitStateHash || state.shortNameHash == DamagedStateHash ||
                                state.tagHash == AttackTagHash || state.shortNameHash == AttackStateHash || 
                                state.shortNameHash == Attack2StateHash || state.shortNameHash == Attack3StateHash ||
                                state.shortNameHash == AttackBStateHash || state.shortNameHash == AttackCStateHash ||
                                state.shortNameHash == EnrageStateHash;

                if (animator.IsInTransition(0))
                {
                    var nextState = animator.GetNextAnimatorStateInfo(0);
                    isLocked |= nextState.tagHash == HitTagHash || nextState.shortNameHash == HitStateHash || nextState.shortNameHash == DamagedStateHash ||
                                nextState.tagHash == AttackTagHash || nextState.shortNameHash == AttackStateHash || 
                                nextState.shortNameHash == Attack2StateHash || nextState.shortNameHash == Attack3StateHash ||
                                nextState.shortNameHash == AttackBStateHash || nextState.shortNameHash == AttackCStateHash ||
                                nextState.shortNameHash == EnrageStateHash;
                }

                if (isLocked)
                {
                    animator.SetFloat(SpeedHash, 0f);
                    return;
                }
            }

            TickBrain(Time.deltaTime);
        }

        protected abstract void TickBrain(float deltaTime);

        protected virtual void HandleFirstHit() { }
        protected virtual void HandleDamaged(float dmg) { }
        protected virtual void HandleStaggered() { }

        public virtual void OnOwnerDied()
        {
            if (isDead) return;
            isDead = true;
            enabled = false;
        }

        void HandleOwnerDied()
        {
            OnOwnerDied();
        }

        protected void MoveTowards(Vector3 worldTarget, float speed)
        {
            if (speed <= 0f) return;

            Vector3 pos = transform.position;
            Vector3 to = worldTarget - pos;
            to.y = 0f;

            if (to.sqrMagnitude <= 0.0001f)
            {
                if (animator != null) animator.SetFloat(SpeedHash, 0f);
                return;
            }

            Vector3 dir = to.normalized;
            float step = speed * Time.deltaTime;
            transform.position = pos + dir * step;

            if (faceTargetWhileMoving)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    rotationSpeedDegPerSec * Time.deltaTime);
            }

            if (animator != null && defaultMoveSpeed > 0f)
            {
                float normalizedSpeed = Mathf.Clamp01(speed / defaultMoveSpeed);
                animator.SetFloat(SpeedHash, normalizedSpeed);
            }
        }


        protected void MoveAwayFrom(Vector3 worldTarget, float speed)
        {
            Vector3 dir = transform.position - worldTarget;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) return;

            Vector3 destination = transform.position + dir.normalized;
            MoveTowards(destination, speed);
        }

        protected void FacePosition(Vector3 worldPos)
        {
            Vector3 to = worldPos - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude <= 0.0001f) return;

            Quaternion targetRot = Quaternion.LookRotation(to.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotationSpeedDegPerSec * Time.deltaTime);
        }

        protected void FaceTarget()
        {
            if (target == null) return;
            FacePosition(target.position);
        }

        protected bool HasLineOfSightToTarget()
        {
            if (!useLineOfSight || target == null) return true;

            Vector3 origin = transform.position + Vector3.up * 1f;
            Vector3 dest = target.position + Vector3.up * 1f;
            Vector3 dir = dest - origin;
            float dist = dir.magnitude - lineOfSightPadding;
            if (dist <= 0f) return true;

            return !Physics.Raycast(
                origin,
                dir.normalized,
                dist,
                lineOfSightMask,
                QueryTriggerInteraction.Ignore);
        }

        protected float DistanceToTargetXZ()
        {
            if (target == null) return float.PositiveInfinity;
            Vector3 a = transform.position;
            Vector3 b = target.position;
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public void ResetSpawnPosition()
        {
            spawnPosition = transform.position;
            spawnPositionInitialized = true;
        }

        protected Vector3 GetRandomPointAroundSpawn(float radius)
        {
            Vector2 rnd = Random.insideUnitCircle * radius;
            return spawnPosition + new Vector3(rnd.x, 0f, rnd.y);
        }

        protected bool IsAttackReady(ref float lastAttackTime, float attackRate)
        {
            if (attackRate <= 0f)
                return false;

            float cooldown = 1f / Mathf.Max(0.001f, attackRate);
            if (Time.time < lastAttackTime + cooldown)
                return false;

            lastAttackTime = Time.time;
            return true;
        }
    }
}

