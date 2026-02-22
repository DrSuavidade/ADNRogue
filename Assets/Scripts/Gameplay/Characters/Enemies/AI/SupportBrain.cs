using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    public class SupportBrain : EnemyBrainBase
    {
        [Header("Positioning")]
        public float followRadiusFromSpawn = 3f;
        public float repositionSpeed = 2f;

        [Header("Support Logic")]
        public float detectionRadius = 25f;
        public float supportInterval = 4f;
        public string supportTrigger = "Support";
        [Range(1, 3)] public int attackVariants = 1;

        float supportTimer;

        protected override void Awake()
        {
            base.Awake();
            supportTimer = supportInterval;
        }

        protected override void TickBrain(float dt)
        {
            supportTimer -= dt;

            // Support enemies tend to hover near their spawn
            if ((transform.position - spawnPosition).sqrMagnitude > followRadiusFromSpawn * followRadiusFromSpawn)
            {
                MoveTowards(spawnPosition, repositionSpeed);
            }
            else if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }

            if (target != null && DistanceToTargetXZ() <= detectionRadius)
                FaceTarget();

            if (supportTimer <= 0f)
            {
                TriggerSupport();
                supportTimer = supportInterval;
            }
        }

        void TriggerSupport()
        {
            if (animator == null || string.IsNullOrEmpty(supportTrigger)) return;

            animator.SetTrigger(supportTrigger);

            // The actual effect (shields, buffs, heals) should live in a
            // separate ability component listening to animation events.
        }
    }
}
