using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    /// <summary>
    /// Base class for all enemy brains. Owns high-level behaviour and locomotion,
    /// but never health/knockback/death (those live on Enemy).
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

        protected PlayerHealth playerHealth;

        protected Vector3 spawnPosition;
        bool spawnPositionInitialized;

        protected bool isDead;

        public float DefaultMoveSpeed   // <- add this
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
            if (enemy != null)
            {
                enemy.OnDamaged += HandleDamaged;
                enemy.OnFirstHit += HandleFirstHit;
                enemy.OnDied += HandleOwnerDied;
            }
        }

        protected virtual void OnDisable()
        {
            if (enemy != null)
            {
                enemy.OnDamaged -= HandleDamaged;
                enemy.OnFirstHit -= HandleFirstHit;
                enemy.OnDied -= HandleOwnerDied;
            }
        }

        protected virtual void Update()
        {
            if (isDead || enemy == null || enemy.IsDead) return;

            TickBrain(Time.deltaTime);
        }

        /// <summary>
        /// Main brain update loop. Called every frame while the enemy is alive.
        /// </summary>
        protected abstract void TickBrain(float deltaTime);

        /// <summary>Called the first time Enemy takes damage.</summary>
        protected virtual void HandleFirstHit() { }

        /// <summary>Called every time Enemy takes damage.</summary>
        protected virtual void HandleDamaged(float dmg) { }

        /// <summary>
        /// Called from Enemy.OnDied and from Enemy.Die().
        /// Override if you need a custom shutdown, but always call base.
        /// </summary>
        public virtual void OnOwnerDied()
        {
            if (isDead) return;
            isDead = true;

            // Stop brain updates.
            enabled = false;
        }

        void HandleOwnerDied()
        {
            OnOwnerDied();
        }

        // --------------------------------------------------------------------
        // Utility helpers for child brains
        // --------------------------------------------------------------------

        protected void MoveTowards(Vector3 worldTarget, float speed)
        {
            if (speed <= 0f) return;

            Vector3 pos = transform.position;
            Vector3 to = worldTarget - pos;
            to.y = 0f;

            if (to.sqrMagnitude <= 0.0001f)
            {
                if (animator != null)
                    animator.SetFloat("Speed", 0f);
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
                animator.SetFloat("Speed", normalizedSpeed);
            }
        }

        protected void MoveAwayFrom(Vector3 worldTarget, float speed)
        {
            Vector3 dir = transform.position - worldTarget;
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) return;

            Vector3 destination = transform.position + dir.normalized; // 1 unit step direction
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

        /// <summary>
        /// Reset the spawn/origin position to the current transform.
        /// Useful if you dynamically move enemies or teleport them.
        /// </summary>
        public void ResetSpawnPosition()
        {
            spawnPosition = transform.position;
            spawnPositionInitialized = true;
        }

        /// <summary>
        /// Picks a random point on the XZ plane around spawnPosition within radius.
        /// </summary>
        protected Vector3 GetRandomPointAroundSpawn(float radius)
        {
            Vector2 rnd = Random.insideUnitCircle * radius;
            return spawnPosition + new Vector3(rnd.x, 0f, rnd.y);
        }

        /// <summary>
        /// Returns true if an attack is ready based on attacks-per-second (attackRate).
        /// Updates lastAttackTime to Time.time when it fires.
        /// </summary>
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
