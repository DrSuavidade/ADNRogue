using UnityEngine;
///Develop
namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanBaker : RomanEnemyAbilityBase
    {
        [Header("Melee (Espátula)")]
        public float damage = 10f;
        public float hitRange = 1.8f;

        // Chamado no frame de impacto da animação
        public void AnimEvent_SpatulaHit()
        {
            DealDamageToPlayer(damage, hitRange);
        }
    }
}

