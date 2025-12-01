using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanGladiator : RomanEnemyAbilityBase
    {
        [Header("Sword Slash")]
        public float slashDamage = 16f;
        public float slashRange = 2.6f;
        [Range(0f, 180f)]
        public float slashAngle = 80f;   // cone à frente

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
