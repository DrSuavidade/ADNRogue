using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    public class FlyingBrain : EnemyBrainBase
    {
        [Header("Flight")]
        public float hoverHeight = 5f;
        public float hoverLerpSpeed = 5f;

        [Header("Orbit")]
        public float orbitRadius = 10f;
        public float orbitAngularSpeedDeg = 60f;

        [Header("Attack Logic")]
        // continuam a existir porque o EnemyConfigurator/flying archetype os usa
        public float attackRate = 1.5f;
        public float preferredRange = 12f;
        [Range(1, 3)] public int attackVariants = 1;

        float orbitAngle;
        float lastAttackTime;

        protected override void TickBrain(float dt)
        {
            if (target == null)
            {
                Hover(dt);
                return;
            }

            Hover(dt);

            float dist = DistanceToTargetXZ();

            if (dist > preferredRange * 1.2f)
            {
                // Aproxima
                MoveTowards(target.position, defaultMoveSpeed);
            }
            else if (dist < preferredRange * 0.8f)
            {
                // Afasta um bocadinho
                MoveAwayFrom(target.position, defaultMoveSpeed);
            }
            else
            {
                // Órbita à volta do alvo
                OrbitTarget(dt);
                FaceTarget();
                TryAttack();
            }
        }

        void Hover(float dt)
        {
            Vector3 pos = transform.position;
            float targetY = hoverHeight;
            pos.y = Mathf.Lerp(pos.y, targetY, hoverLerpSpeed * dt);
            transform.position = pos;
        }

        void OrbitTarget(float dt)
        {
            if (target == null) return;

            orbitAngle += orbitAngularSpeedDeg * dt;
            float rad = orbitAngle * Mathf.Deg2Rad;

            Vector3 center = target.position;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * orbitRadius;
            Vector3 desiredPos = new Vector3(center.x + offset.x, transform.position.y, center.z + offset.z);

            MoveTowards(desiredPos, defaultMoveSpeed);
        }

        void TryAttack()
        {
            if (!IsAttackReady(ref lastAttackTime, attackRate))
                return;

            TriggerAttackAnim();
            // Aqui é onde podes:
            // - disparar projéteis
            // - chamar uma ability
            // - ou simplesmente não fazer nada por agora
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
