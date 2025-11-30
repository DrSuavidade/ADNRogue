using UnityEngine;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    /// <summary>
    /// Shared utility base for prehistoric enemy abilities.
    /// Handles caching Enemy, player Transform & PlayerHealth.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class PrehistoricEnemyAbilityBase : MonoBehaviour
    {
        protected Enemy enemy;
        protected Transform self;
        protected Transform target;
        protected PlayerHealth playerHealth;

        protected virtual void Awake()
        {
            enemy = GetComponent<Enemy>();
            self = transform;

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
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

        protected void DealDamageToPlayer(float damage, float range)
        {
            if (playerHealth == null || !IsPlayerInRange(range)) return;
            playerHealth.ApplyDamage(damage);
        }
    }
}
