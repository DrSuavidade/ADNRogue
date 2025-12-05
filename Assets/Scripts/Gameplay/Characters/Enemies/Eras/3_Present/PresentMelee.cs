using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Present
{
    [RequireComponent(typeof(EnemyCore))]
    public class PresentMelee : PresentEnemyAbilityBase
    {
        [Header("Melee  Pé de cabra")]
        public float crowbarDamage = 10f;
        public float crowbarRange = 2.0f;

        // Animation Event quando a arma acerta
        public void AnimEvent_PrimaryAttack()
        {
            DealDamageToPlayer(crowbarDamage, crowbarRange);
        }
    }
}
