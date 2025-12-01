using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanPainter : RomanEnemyAbilityBase
    {
        [Header("Arcane Brush Wave")]
        public float waveRadius = 9f;
        public float damage = 6f;

        // Evento na animação quando o pincel "explode" magia
        public void AnimEvent_BrushCast()
        {
            // Neste modelo, a "magia" é uma explosão circular centrada no painter
            DealDamageToPlayer(damage, waveRadius);

            // Aqui podes ligar VFX (spawnar prefab, partículas, etc.)
        }
    }
}
