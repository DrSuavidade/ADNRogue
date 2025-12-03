using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    public class AnimalBrain : EnemyBrainBase
    {
        [Header("Roaming")]
        public float roamRadius = 8f;
        public float roamInterval = 2f;
        public float roamSpeed = 3f;

        [Header("Aggro")]
        public float detectionRadius = 18f;
        public float pounceRange = 4f;   // distância a que começa a atacar
        public float chaseSpeed = 5f;

        [Header("Attack")]
        public float attackRate = 1f;    // ataques por segundo

        [Header("Animation")]
        public string attackTrigger = "Attack";  // TEM de existir no Animator

        Vector3 roamTarget;
        float roamTimer;
        float lastAttackTime;

        protected override void Awake()
        {
            base.Awake();
            PickRoamTarget();
        }

        protected override void TickBrain(float dt)
        {
            if (target == null)
            {
                TickRoam(dt);
                return;
            }

            float dist = DistanceToTargetXZ();
            if (dist > detectionRadius)
            {
                TickRoam(dt);
                return;
            }

            if (dist > pounceRange)
            {
                // Perseguir o alvo
                MoveTowards(target.position, chaseSpeed);
            }
            else
            {
                // Dentro de alcance de ataque
                FaceTarget();
                TryAttack();
            }
        }

        void TickRoam(float dt)
        {
            roamTimer -= dt;
            if (roamTimer <= 0f)
                PickRoamTarget();

            MoveTowards(roamTarget, roamSpeed);
        }

        void PickRoamTarget()
        {
            roamTimer = roamInterval;
            roamTarget = GetRandomPointAroundSpawn(roamRadius);
        }

        void TryAttack()
        {
            // Usa helper do EnemyBrainBase para cooldown
            if (!IsAttackReady(ref lastAttackTime, attackRate))
                return;

            if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            {
                animator.SetTrigger(attackTrigger);
            }
            // O dano é tratado via Animation Event em PrehistoricTRex.
        }
    }
}
