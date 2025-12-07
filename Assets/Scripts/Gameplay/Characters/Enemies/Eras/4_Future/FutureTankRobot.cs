using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Future
{
    [RequireComponent(typeof(EnemyCore))]
    public class FutureTankRobot : FutureEnemyAbilityBase
    {
        [Header("Melee – Punhos de Aço")]
        [Tooltip("Dano de cada soco do Tank Robot.")]
        public float punchDamage = 18f;

        [Tooltip("Alcance do soco (raio em metros).")]
        public float punchRange = 2.5f;

        // Animation Event no frame em que o punho deve acertar:
        // chama isto: AnimEvent_PrimaryAttack
        public void AnimEvent_PrimaryAttack()
        {
            DealDamageToPlayer(punchDamage, punchRange);
        }
    }
}
