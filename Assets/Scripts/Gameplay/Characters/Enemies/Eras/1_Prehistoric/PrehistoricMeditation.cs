using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    public class PrehistoricMeditation : PrehistoricEnemyAbilityBase
    {
        [Header("Meditation Aura")]
        public float radius = 12f;
        public float healPerCast = 6f;
        public LayerMask allyMask = ~0;

        // Animation event: when the meditate pulse peaks
        public void AnimEvent_Meditate()
        {
            var cols = Physics.OverlapSphere(transform.position, radius, allyMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].attachedRigidbody && cols[i].attachedRigidbody.gameObject == gameObject)
                    continue;

                var e = cols[i].GetComponentInParent<EnemyCore>();
                if (!e || e == enemy) continue;
                if (e.CurrentHealth <= 0f) continue;

                // how much we can actually heal without exceeding max
                float delta = Mathf.Min(healPerCast, e.MaxHealth - e.CurrentHealth);
                if (delta <= 0f) continue;

                // use the proper API instead of reflection
                e.Heal(delta);
            }
        }
    }
}
