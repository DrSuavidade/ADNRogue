using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanPriest : RomanEnemyAbilityBase
    {
        [Header("Holy Nova")]
        public float novaRadius = 10f;
        public float damage = 5f;

        [Header("Optional Ally Heal")]
        public bool healAllies = true;
        public float healPerCast = 4f;
        public LayerMask allyMask = ~0;

        // Evento de animação quando o feitiço "explode"
        public void AnimEvent_HolyNova()
        {
            // Dano ao player, se estiver perto
            DealDamageToPlayer(damage, novaRadius);

            // Opcional: cura leve aos aliados Roman à volta
            if (!healAllies) return;

            var cols = Physics.OverlapSphere(transform.position, novaRadius, allyMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].attachedRigidbody && cols[i].attachedRigidbody.gameObject == gameObject)
                    continue;

                var e = cols[i].GetComponentInParent<EnemyCore>();
                if (!e || e == enemy) continue;
                if (e.CurrentHealth <= 0f) continue;

                float delta = Mathf.Min(healPerCast, e.MaxHealth - e.CurrentHealth);
                if (delta <= 0f) continue;

                e.Heal(delta);
            }
        }
    }
}
