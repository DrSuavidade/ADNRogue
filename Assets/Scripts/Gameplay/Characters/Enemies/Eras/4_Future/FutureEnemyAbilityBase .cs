using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Future
{
    /// <summary>
    /// Base partilhada para as habilidades dos inimigos da era Futuro.
    /// Igual às outras EnemyAbilityBase: cache de EnemyCore, Transform, Player e PlayerHealth.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class FutureEnemyAbilityBase : MonoBehaviour
    {
        protected EnemyCore enemy;
        protected Transform self;
        protected Transform target;
        protected PlayerHealth playerHealth;

        protected virtual void Awake()
        {
            enemy = GetComponent<EnemyCore>();
            self  = transform;

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target       = playerObj.transform;
                playerHealth = playerObj.GetComponent<PlayerHealth>();
            }
        }

        protected bool IsPlayerInRange(float range)
        {
            if (!target) return false;

            Vector3 a = self.position;
            Vector3 b = target.position;
            a.y = b.y = 0f;

            return Vector3.Distance(a, b) <= range;
        }

        /// <summary>
        /// Se o player estiver em range, aplica dano directo.
        /// </summary>
        protected void DealDamageToPlayer(float damage, float range)
        {
            if (playerHealth == null || !IsPlayerInRange(range)) return;
            playerHealth.ApplyDamage(damage);
        }
    }
}
