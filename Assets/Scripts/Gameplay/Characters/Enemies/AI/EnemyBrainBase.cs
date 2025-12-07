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

        protected PlayerHealth playerHealth;

        protected Vector3 spawnPosition;
        bool spawnPositionInitialized;

        protected bool isDead;

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

        // EnemyBrainBase.cs
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

    // 👉 Se estiver na animação de Hit, não corremos o cérebro (não há movimento)
    if (animator != null)
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);

        // Usa o nome OU a tag "Hit" – ajusta ao que tens no Animator
        if (state.IsName("Hit") || state.IsTag("Hit"))
        {
            return;
        }
    }

    TickBrain(Time.deltaTime);
}

        protected abstract void TickBrain(float deltaTime);

        protected virtual void HandleFirstHit() { }
        protected virtual void HandleDamaged(float dmg) { }

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

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

     protected void MoveTowards(Vector3 worldTarget, float speed)
{
    if (speed <= 0f) return;

    // ⬇️ NOVO: se o Animator estiver numa animação de ataque, não mexemos o inimigo
    if (animator != null)
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);

        // Se o estado atual tiver nome/tag "Attack", não mover
        if (state.IsName("Attack") || state.IsTag("Attack"))
        {
            animator.SetFloat("Speed", 0f);
            return;
        }
    }
    // ⬆️ FIM DO NOVO BLOCO

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
