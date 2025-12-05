using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Present
{
    [RequireComponent(typeof(EnemyCore))]
    public class PresentLady : PresentEnemyAbilityBase
    {
        [Header("Melee Punhos")]
        public float punchDamage = 7f;
        public float punchRange = 1.8f;

        // Animation Event quando o soco acerta
        public void AnimEvent_PrimaryAttack()
        {
            DealDamageToPlayer(punchDamage, punchRange);
        }
    }
}
