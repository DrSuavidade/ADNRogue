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
        public Sprite[] enrageAnimationFrames;
        public float enrageFPS = 10f;
        public Vector3 enrageScale = new Vector3(2.5f, 2.5f, 1f);
        public float enrageYOffset = 1.2f;
        [ColorUsage(true, true)] public Color enrageColor = new Color(2.5f, 0.8f, 0.2f, 1.0f); // Fire Orange Glow (HDR)

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

            // 2. Spawn Enrage Visual FX (Professional Layered approach)
            if (enrageAnimationFrames != null && enrageAnimationFrames.Length > 0)
            {
                Vector3 spawnPos = transform.position + Vector3.up * enrageYOffset;
                
                // CAMADA 1: Burst inicial (mais rápido e rotacionado)
                SpawnVFXLayer("Gladiator_Enrage_Burst", spawnPos, enrageScale * 1.5f, enrageAnimationFrames, enrageFPS * 1.5f, enrageColor, 1.2f, 360f);

                // CAMADA 2: Aura persistente que segue o gladiador
                SpawnVFXLayer("Gladiator_Enrage_Aura", spawnPos, enrageScale, enrageAnimationFrames, enrageFPS, enrageColor * 0.8f, 1.0f, 0f, 0.5f, true, this.transform);

                // CAMADA 3: Glow residual no chão/corpo
                SpawnVFXLayer("Gladiator_Enrage_Glow", spawnPos, enrageScale * 2f, new Sprite[] { enrageAnimationFrames[0] }, 1f, enrageColor * 0.4f, 1.1f, 0f, 0.2f, true, this.transform);
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
