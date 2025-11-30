using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    public class RangedBrain : EnemyBrainBase
    {
        [Header("Wander")]
        public float wanderRadius = 6f;
        public float wanderInterval = 4f;
        public float wanderSpeed = 2f;

        [Header("Engagement")]
        public float detectionRadius = 25f;
        public float preferredRange = 12f;
        public float minRange = 6f;
        public float strafeSpeed = 3f;

        [Header("Attack")]
        public float attackRate = 1.25f;
        public string attackTrigger = "Attack";

        Vector3 spawnPos;
        Vector3 wanderTarget;
        float wanderTimer;
        float lastAttackTime;

        protected override void Awake()
        {
            base.Awake();
            spawnPos = transform.position;
            PickWanderTarget();
        }

        protected override void TickBrain(float dt)
        {
            if (target == null)
            {
                TickWander(dt);
                return;
            }

            float dist = DistanceToTargetXZ();

            if (dist > detectionRadius)
            {
                TickWander(dt);
                return;
            }

            bool hasLOS = HasLineOfSightToTarget();

            if (dist > preferredRange)
            {
                // close in
                MoveTowards(target.position, wanderSpeed * 1.5f);
            }
            else if (dist < minRange)
            {
                // back away
                MoveAwayFrom(target.position, wanderSpeed * 1.5f);
            }
            else
            {
                // in ideal range: strafe and shoot
                FaceTarget();
                Strafe(dt);

                if (hasLOS)
                    TryAttack();
            }
        }

        void TickWander(float dt)
        {
            wanderTimer -= dt;
            if (wanderTimer <= 0f)
                PickWanderTarget();

            MoveTowards(wanderTarget, wanderSpeed);
        }

        void PickWanderTarget()
        {
            wanderTimer = wanderInterval;
            Vector2 rnd = UnityEngine.Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void Strafe(float dt)
        {
            if (target == null) return;

            // simple orbit: move perpendicular to direction to target
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f) return;

            Vector3 right = Vector3.Cross(Vector3.up, toTarget.normalized);
            Vector3 strafeDest = transform.position + right * 1f;
            MoveTowards(strafeDest, strafeSpeed);
        }

        void TryAttack()
        {
            if (Time.time < lastAttackTime + 1f / Mathf.Max(0.001f, attackRate))
                return;

            lastAttackTime = Time.time;

            if (animator != null && !string.IsNullOrEmpty(attackTrigger))
                animator.SetTrigger(attackTrigger);

            // Actual projectile spawn should be handled by an ability component
            // via animation events or a hook like OnRangedAttackFired().
        }
    }
}
