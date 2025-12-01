using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanMaceMaster : RomanEnemyAbilityBase
    {
        [Header("Mace Slam")]
        public float slamDamage = 20f;
        public float slamRadius = 2.4f;

        // Golpe pesado no chão, circular
        public void AnimEvent_MaceSlam()
        {
            DealDamageToPlayer(slamDamage, slamRadius);
        }
    }
}
