using UnityEngine;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    public class PrehistoricTRex : PrehistoricEnemyAbilityBase
    {
        [Header("Bite Settings")]
        public float biteDamage = 20f;
        public Transform bitePoint;
        public float biteRadius = 0.9f;
        public LayerMask playerMask;

        readonly Collider[] _hitBuf = new Collider[4];

        // Chamado pelo Animation Event "AnimEvent_Bite"
        public void AnimEvent_Bite()
        {
            if (bitePoint == null)
            {
                Debug.LogWarning($"{name}: PrehistoricTRex sem bitePoint atribuído.");
                return;
            }

            int n = Physics.OverlapSphereNonAlloc(
                bitePoint.position,
                biteRadius,
                _hitBuf,
                playerMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < n; i++)
            {
                var ph = _hitBuf[i].GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    ph.ApplyDamage(biteDamage);
                    break;
                }
            }
        }
    }
}
