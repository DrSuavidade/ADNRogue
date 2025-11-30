using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(Enemy))]
    public class PrehistoricTRex : PrehistoricEnemyAbilityBase
    {
        [Header("Bite")]
        public float biteDamage = 20f;
        public float biteRange = 2.5f;

        // Animation event
        public void AnimEvent_Bite()
        {
            DealDamageToPlayer(biteDamage, biteRange);
        }
    }
}
