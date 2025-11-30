using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(Enemy))]
    public class PrehistoricCavemanMelee : PrehistoricEnemyAbilityBase
    {
        [Header("Melee")]
        public float damage = 8f;
        public float hitRange = 1.8f;

        // Called by animation event at the moment the stick should hit
        public void AnimEvent_PrimaryAttack()
        {
            DealDamageToPlayer(damage, hitRange);
        }
    }
}
