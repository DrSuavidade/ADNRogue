using UnityEngine;
using System;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    public class MeleeBrain : EnemyBrainBase
    {
        [Header("Behavior")]
        public bool stayStationary = false;
        public bool rotateInPlace = true;

        [Header("Wander Settings")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;

        [Header("Chase & Attack")]
        public float detectionRadius = 20f;
        public float chaseSpeed = 4f;
        public float attackRange = 1.5f;
        public float attackRate = 1f;
        public float damagePerHit = 10f;

        [Header("Attack Variants")]
        [Tooltip("1 = Attack | 2 = Attack/Attack2 | 3 = Attack/Attack2/Attack3")]
        [Range(1, 3)]
        public int attackVariants = 1;

        [Header("Damage Pause")]
        public float damagePauseDuration = 0.5f;

        Vector3 wanderTarget;
        float wanderTimer;
        float lastAttackTime;

        enum State { Wandering, Chasing, Attacking }

        [SerializeField, Tooltip("Debug: current AI state")]
        State state = State.Wandering;


        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        protected override void Awake()
        {
            base.Awake();
            PickWanderTarget();
        }

        protected override void HandleStaggered()
        {
            isDamagePaused = true;
            damagePauseTimer = 0f;
        }

        protected override void TickBrain(float dt)
        {
            if (target == null)
            {
                TickWander(dt);
                return;
            }

            if (isDamagePaused)
            {
                damagePauseTimer += dt;
                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
            }

            float dist = DistanceToTargetXZ();

            if (stayStationary)
            {
                if (rotateInPlace)
                    FaceTarget();

                if (dist <= attackRange)
                    TryAttack();
                else if (animator != null)
                    animator.SetFloat("Speed", 0f);

                return;
            }

            if (dist > detectionRadius)
            {
                TickWander(dt);
            }
            else if (dist > attackRange)
            {
                DebugState(State.Chasing);
                MoveTowards(target.position, chaseSpeed);
            }
            else
            {
                DebugState(State.Attacking);
                if (animator != null) animator.SetFloat("Speed", 0f);
                FaceTarget();
                TryAttack();
            }
        }

        void TickWander(float dt)
        {
            DebugState(State.Wandering);
            wanderTimer -= dt;
            if (wanderTimer <= 0f)
            {
                PickWanderTarget();
            }

            MoveTowards(wanderTarget, wanderSpeed);
        }

        void PickWanderTarget()
        {
            wanderTimer = wanderInterval;
            wanderTarget = GetRandomPointAroundSpawn(wanderRadius);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void DebugState(State s)
        {
            // Read the state in editor builds so CS0414 goes away and you can breakpoint/inspect.
            if (state != s) state = s;
        }



        void TryAttack()
        {
            if (!IsAttackReady(ref lastAttackTime, attackRate))
                return;

            // Anti-backfire check
            if (target != null)
            {
                Vector3 toTarget = (target.position - transform.position);
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    float angle = Vector3.Angle(transform.forward, toTarget.normalized);
                    if (angle > 45f) return;
                }
            }

            // NEW: Use MeleeAttackAbility if available (Professional Hitbox/Timing)
            var ability = GetComponent<Geneforge.Gameplay.Characters.Enemies.Abilities.MeleeAttackAbility>();
            if (ability != null)
            {
                ability.SetTarget(target);
                ability.Configure(damagePerHit, attackRange);
                ability.BeginAttack(); // Starts animation + internal timer or waits for event
                
                // Trigger animation
                TriggerAttackAnim();
                return;
            }

            // OLD: Instant damage fallback (Programmer Art style)
            if (animator != null)
            {
                TriggerAttackAnim();
            }

            // Optional direct damage (if you don't rely on animation events/hitboxes)
            if (playerHealth != null && DistanceToTargetXZ() <= attackRange + 0.1f)
            {
                playerHealth.ApplyDamage(damagePerHit);
            }
        }

        void TriggerAttackAnim()
        {
            if (animator == null) return;

            if (attackVariants <= 1)
            {
                animator.SetTrigger("Attack");
            }
            else
            {
                int idx = UnityEngine.Random.Range(0, attackVariants);
                string suffix = idx == 0 ? "" : (idx + 1).ToString(); // Attack, Attack2, Attack3
                animator.SetTrigger("Attack" + suffix);
            }
        }
    }
}