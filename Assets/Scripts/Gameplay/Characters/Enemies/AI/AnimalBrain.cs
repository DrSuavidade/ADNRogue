using UnityEngine;
using System;

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
        public float pounceRange = 4f;
        public float chaseSpeed = 5f;

        [Header("Animation")]
        public string pounceTrigger = "Pounce";

        Vector3 spawnPos;
        Vector3 roamTarget;
        float roamTimer;

        protected override void Awake()
        {
            base.Awake();
            spawnPos = transform.position;
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
                MoveTowards(target.position, chaseSpeed);
            }
            else
            {
                FaceTarget();
                TryPounce();
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
            Vector2 rnd = UnityEngine.Random.insideUnitCircle * roamRadius;
            roamTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void TryPounce()
        {
            if (animator != null && !string.IsNullOrEmpty(pounceTrigger))
                animator.SetTrigger(pounceTrigger);

            // Pounce damage / movement handled by the ability component.
        }
    }
}
