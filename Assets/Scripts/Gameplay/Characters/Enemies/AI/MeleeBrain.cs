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
        [Tooltip("1 = Attack | 2 = Attack/AttackB | 3 = Attack/AttackB/AttackC")]
        [Range(1, 3)]
        public int attackVariants = 3;

        [Header("Damage Pause")]
        public float damagePauseDuration = 0.5f;

        Vector3 wanderTarget;
        float wanderTimer;
        float lastAttackTime;

        enum State { Wandering, Chasing, Attacking }
        State state = State.Wandering;

        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        protected override void Awake()
        {
            base.Awake();
            PickWanderTarget();
        }

        protected override void HandleDamaged(float dmg)
        {
            base.HandleDamaged(dmg);
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
                state = State.Chasing;
                MoveTowards(target.position, chaseSpeed);
            }
            else
            {
                state = State.Attacking;
                if (animator != null) animator.SetFloat("Speed", 0f);
                FaceTarget();
                TryAttack();
            }
        }

        void TickWander(float dt)
        {
            state = State.Wandering;
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


        void TryAttack()
        {
            if (!IsAttackReady(ref lastAttackTime, attackRate))
                return;

            if (animator != null)
            {
                if (attackVariants <= 1)
                {
                    animator.SetTrigger("Attack");
                }
                else
                {
                    int idx = UnityEngine.Random.Range(0, attackVariants);
                    switch (idx)
                    {
                        default:
                        case 0: animator.SetTrigger("Attack"); break;
                        case 1: animator.SetTrigger("AttackB"); break;
                        case 2: animator.SetTrigger("AttackC"); break;
                    }
                }
            }

            // Optional direct damage (if you don't rely on animation events/hitboxes)
            if (playerHealth != null && DistanceToTargetXZ() <= attackRange + 0.1f)
            {
                playerHealth.ApplyDamage(damagePerHit);
            }
        }
    }
}