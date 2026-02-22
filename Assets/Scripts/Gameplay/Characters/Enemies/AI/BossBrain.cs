using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.AI
{
    public class BossBrain : EnemyBrainBase
    {
        [Header("Phases")]
        [Tooltip("HP fraction where boss enters phase 2 (e.g. 0.66).")]
        public float phase2Threshold = 0.66f;
        [Tooltip("HP fraction where boss enters phase 3 (e.g. 0.33).")]
        public float phase3Threshold = 0.33f;

        [Header("Phase Timings")]
        public float phase1AttackRate = 1.2f;
        public float phase2AttackRate = 0.9f;
        public float phase3AttackRate = 0.6f;

        [Header("Animation")]
        public string phase1AttackTrigger = "Attack";
        public string phase2AttackTrigger = "AttackB";
        public string phase3AttackTrigger = "AttackC";
        public string phaseChangeTrigger = "PhaseChange";
        [Range(1, 3)] public int attackVariants = 1;

        int currentPhase = 1;
        float lastAttackTime;

        protected override void TickBrain(float dt)
        {
            if (enemy == null || target == null) return;

            UpdatePhase();

            FaceTarget();

            // Anti-backfire check: Wait until boss is facing the player
            Vector3 toTarget = (target.position - transform.position);
            toTarget.y = 0;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.Angle(transform.forward, toTarget.normalized);
                if (angle > 40f) return;
            }

            float attackRate = GetCurrentAttackRate();
            if (IsAttackReady(ref lastAttackTime, attackRate))
            {
                PerformPhaseAttack();
            }

        }

        void UpdatePhase()
        {
            if (enemy == null || enemy.MaxHealth <= 0f) return;

            float hpFrac = enemy.CurrentHealth / enemy.MaxHealth;
            int newPhase = currentPhase;

            if (hpFrac <= phase3Threshold)
                newPhase = 3;
            else if (hpFrac <= phase2Threshold)
                newPhase = 2;
            else
                newPhase = 1;

            if (newPhase != currentPhase)
            {
                currentPhase = newPhase;
                OnPhaseChanged(newPhase);
            }
        }

        void OnPhaseChanged(int newPhase)
        {
            if (animator != null && !string.IsNullOrEmpty(phaseChangeTrigger))
                animator.SetTrigger(phaseChangeTrigger);

            // Hook here for phase-specific ability enabling/disabling:
            // e.g. enable lava pools in phase 2, add new patterns in phase 3.
        }

        float GetCurrentAttackRate()
        {
            switch (currentPhase)
            {
                default:
                case 1: return phase1AttackRate;
                case 2: return phase2AttackRate;
                case 3: return phase3AttackRate;
            }
        }

        void PerformPhaseAttack()
        {
            if (animator == null) return;

            string baseTrigger = "";
            switch (currentPhase)
            {
                default:
                case 1: baseTrigger = phase1AttackTrigger; break;
                case 2: baseTrigger = phase2AttackTrigger; break;
                case 3: baseTrigger = phase3AttackTrigger; break;
            }

            if (string.IsNullOrEmpty(baseTrigger)) return;

            if (attackVariants <= 1)
            {
                animator.SetTrigger(baseTrigger);
            }
            else
            {
                int idx = UnityEngine.Random.Range(0, attackVariants);
                string suffix = idx == 0 ? "" : (idx + 1).ToString(); // Attack, Attack2, Attack3
                animator.SetTrigger(baseTrigger + suffix);
            }

            // Actual patterns (AOEs, combos, etc.) should be implemented
            // as abilities subscribed to animation events for each phase.
        }

        public override void OnOwnerDied()
        {
            base.OnOwnerDied();
            // Extra clean-up for boss (cutscenes, doors, etc.) can go here.
        }
    }
}
