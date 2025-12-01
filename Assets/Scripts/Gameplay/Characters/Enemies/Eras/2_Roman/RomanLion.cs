using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanLion : RomanEnemyAbilityBase
    {
        [Header("Lion Bite / Claw")]
        [Tooltip("Dano do ataque corpo-a-corpo do leão.")]
        public float biteDamage = 14f;

        [Tooltip("Raio máximo em que o leão consegue atingir o jogador.")]
        public float biteRange = 2.2f;

        [Tooltip("Ângulo do cone à frente do leão em que o ataque acerta.")]
        [Range(0f, 180f)]
        public float biteAngle = 70f;

        /// <summary>
        /// Chamado por evento de animação no frame de impacto (ex: 'AnimEvent_LionBite').
        /// </summary>
        public void AnimEvent_LionBite()
        {
            if (!target || playerHealth == null) return;

            // Distância no plano XZ
            Vector3 toPlayer = target.position - self.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > biteRange) return;

            // Verifica se o jogador está dentro do cone à frente do leão
            Vector3 forward = self.forward;
            forward.y = 0f;

            float angle = Vector3.Angle(forward, toPlayer);
            if (angle <= biteAngle * 0.5f)
            {
                playerHealth.ApplyDamage(biteDamage);
            }
        }
    }
}
