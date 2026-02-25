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
        public float enrageFPS = 14f;
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

            // 2. Spawn Enrage Visual FX (Dynamic)
            if (enrageAnimationFrames != null && enrageAnimationFrames.Length > 0)
            {
                Vector3 spawnPos = transform.position + Vector3.up * enrageYOffset;
                
                GameObject fireObj = new GameObject("Gladiator_Enrage_VFX");
                fireObj.transform.position = spawnPos;
                fireObj.transform.SetParent(this.transform); // Segue o gladiador
                fireObj.transform.localScale = enrageScale;

                var sr = fireObj.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 50; // Garante que cobre o gladiador

                var animator = fireObj.AddComponent<Geneforge.Gameplay.Visuals.SpriteSheetAnimator>();
                animator.useSpawnScale = true;
                animator.usePulse = true;
                animator.tintColor = enrageColor * 1.5f; 
                animator.loop = false; 

                // BILLBOARD: Faz o enrage "cobrir" o gladiador vindo da câmara
                animator.Initialize(enrageAnimationFrames, enrageFPS, Geneforge.Gameplay.Visuals.SpriteSheetAnimator.AnimationMode.Billboard);

                // 3. Destruir automaticamente após a duração da animação
                float duration = enrageAnimationFrames.Length / (enrageFPS > 0 ? enrageFPS : 14f);
                Destroy(fireObj, duration + 0.5f); 
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
