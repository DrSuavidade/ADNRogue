using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(Enemy))]
    public class PrehistoricMuscles : PrehistoricEnemyAbilityBase
    {
        [Header("Heavy Slam")]
        public float slamDamage = 18f;
        public float slamRange = 2.8f;
        [Range(0f, 180f)]
        public float slamAngle = 80f; // cone in front

        // Animation event
        public void AnimEvent_HeavySlam()
        {
            if (!target || playerHealth == null) return;

            // check if player is in a cone in front
            Vector3 toPlayer = target.position - self.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > slamRange) return;

            Vector3 fwd = self.forward;
            fwd.y = 0f;

            float angle = Vector3.Angle(fwd, toPlayer);
            if (angle <= slamAngle * 0.5f)
            {
                playerHealth.ApplyDamage(slamDamage);
            }
        }
    }
}
