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

                var e = cols[i].GetComponentInParent<Enemy>();
                if (!e || e == enemy) continue;
                if (e.CurrentHealth <= 0f) continue;

                // simple heal – clamp to max
                float newHp = Mathf.Min(e.CurrentHealth + healPerCast, e.MaxHealth);
                float delta = newHp - e.CurrentHealth;
                if (delta <= 0f) continue;

                // reuse TakeDamage as a negative damage? Better to expose a Heal,
                // but for now we do a simple field adjust if you prefer.
                var field = typeof(Enemy).GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    field.SetValue(e, newHp);
            }
        }
    }
}
