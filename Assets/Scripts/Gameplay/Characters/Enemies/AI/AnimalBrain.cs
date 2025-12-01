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

        Vector3 roamTarget;
        float roamTimer;

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
            roamTarget = GetRandomPointAroundSpawn(roamRadius);
        }


        void TryPounce()
        {
            if (animator != null && !string.IsNullOrEmpty(pounceTrigger))
                animator.SetTrigger(pounceTrigger);

            // Pounce damage / movement handled by the ability component.
        }
    }
}
