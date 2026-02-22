using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    public class PaintProjectile : MonoBehaviour
    {
        public float damage;
        public LayerMask hitMask;

        private void OnTriggerEnter(Collider other)
        {
            // 1. IGNORAR se bater no próprio Pintor ou noutros Inimigos (para não explodir na mão dele)
            if (other.GetComponentInParent<EnemyCore>() != null) return;

            // 2. SE BATER NO PLAYER (conforme definido no Hit Mask do Inspector)
            if (((1 << other.gameObject.layer) & hitMask) != 0) // Tenta dar dano ao player
            {
                var health = other.GetComponentInParent<PlayerHealth>();
                if (health != null)
                {
                    health.ApplyDamage(damage);
                    Debug.Log($"<color=red>[PAINT] Dano aplicado: {damage}</color>");
                }
                Impact();
            }
            // 3. SE BATER NO CENÁRIO (Layer Default ou Tag Environment)
            else if (other.gameObject.layer == 0 || other.CompareTag("Environment"))
            {
                Impact();
            }
        }

        private void Impact()
        {
            // Opcional: Instanciar Splash de tinta aqui
            Destroy(gameObject);
        }
    }
}
