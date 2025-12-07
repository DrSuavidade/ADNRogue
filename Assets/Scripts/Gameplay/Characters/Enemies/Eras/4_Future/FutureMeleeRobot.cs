using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Future
{
    [RequireComponent(typeof(EnemyCore))]
    public class FutureMeleeRobot : FutureEnemyAbilityBase
    {
        [Header("Melee – Espada de Energia")]
        [Tooltip("Dano de cada golpe de espada.")]
        public float swordDamage = 18f;

        [Tooltip("Alcance do golpe (raio em metros).")]
        public float swordRange = 2.5f;

        // Animation Event no frame em que a espada deve acertar:
        // chama isto na animação: AnimEvent_SwordSlash
        public void AnimEvent_SwordSlash()
        {
            DealDamageToPlayer(swordDamage, swordRange);
        }
    }
}
