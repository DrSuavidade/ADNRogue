using UnityEngine;
///Develop
namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    public class PrehistoricMountGirl : PrehistoricEnemyAbilityBase
    {
        [Header("Spear Thrust")]
        public float spearDamage = 10f;
        public float spearRange = 2.2f;

        [Header("Charge")]
        public float chargeDamage = 16f;
        public float chargeWidth = 2.0f;
        public float chargeLength = 6.0f;

        // Animation event for spear
        public void AnimEvent_SpearThrust()
        {
            DealDamageToPlayer(spearDamage, spearRange);
        }

        // Animation event for charge impact (e.g. when the mount hits peak)
        public void AnimEvent_ChargeImpact()
        {
            if (!target || playerHealth == null) return;

            // check if player lies within a forward-oriented box
            Vector3 origin = self.position;
            Vector3 toPlayer = target.position - origin;
            toPlayer.y = 0f;

            Vector3 fwd = self.forward;
            fwd.y = 0f;
            float forwardDist = Vector3.Dot(toPlayer, fwd);
            if (forwardDist < 0f || forwardDist > chargeLength) return;

            // lateral distance
            Vector3 lateral = toPlayer - fwd * forwardDist;
            if (lateral.magnitude > chargeWidth * 0.5f) return;

            playerHealth.ApplyDamage(chargeDamage);
        }
    }
}
