using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanGladiator : RomanEnemyAbilityBase
    {
        [Header("Enrage (One-time)")]
        public float enrageDetectionRange = 12f;
        private bool hasEnraged = false;

        [Header("Enrage Visuals")]
        public GameObject enragePrefab;
        public float enrageScaleMult = 1.0f;
        public float enrageYOffset = 1.2f;

        [Header("Sword Slash")]
        public float slashDamage = 16f;
        public float slashRange = 2.6f;
        [Range(0f, 180f)]
        public float slashAngle = 80f;   // cone à frente

        private void Update()
        {
            if (enemy == null || enemy.IsDead) return;

            // Trigger Enrage once when player is detected
            if (!hasEnraged && IsPlayerInRange(enrageDetectionRange))
            {
                TriggerEnrage();
            }
        }

        private void TriggerEnrage()
        {
            hasEnraged = true;
            
            // 1. Trigger Animator
            if (enemy.Animator != null)
            {
                enemy.Animator.SetTrigger("Enrage");
            }

            // 2. Spawn Enrage Visual FX
            if (enragePrefab != null)
            {
                Vector3 spawnPos = transform.position + Vector3.up * enrageYOffset;
                SpawnVFX(enragePrefab, spawnPos, Quaternion.identity, transform, enrageScaleMult);
            }

        }

        // Evento na animação de ataque
        public void AnimEvent_SwordSlash()
        {
            if (!target || playerHealth == null) return;

            Vector3 toPlayer = target.position - self.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > slashRange) return;

            Vector3 fwd = self.forward;
            fwd.y = 0f;

            float angle = Vector3.Angle(fwd, toPlayer);
            if (angle <= slashAngle * 0.5f)
            {
                playerHealth.ApplyDamage(slashDamage);
            }
        }
    }
}
