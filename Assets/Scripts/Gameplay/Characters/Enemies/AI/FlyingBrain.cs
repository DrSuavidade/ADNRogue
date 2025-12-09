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
                TryAttack(); // aqui NÃO usamos Animator
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

            // Aqui é onde podes:
            // - disparar projéteis
            // - chamar uma ability
            // - ou simplesmente não fazer nada por agora
            //
            // NÃO há qualquer chamada a Animator aqui.
        }
    }
}
