using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    public class PrehistoricDrummer : PrehistoricEnemyAbilityBase
    {
        [Header("Sound Wave")]
        public float waveRadius = 10f;
        public float damage = 4f;
        public LayerMask hitMask = ~0;

        // Animation event: called when drum is struck
        public void AnimEvent_DrumBeat()
        {
            if (!target) return;

            // Damage player if inside radius
            DealDamageToPlayer(damage, waveRadius);

            // Optional: lightly knock back enemies or the player
            // (you can expand this to shake camera, etc.)
        }
    }
}
