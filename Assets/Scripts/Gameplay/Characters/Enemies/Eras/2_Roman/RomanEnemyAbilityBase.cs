using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Core.Pooling;
using System.Collections;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    /// <summary>
    /// Base partilhada para as habilidades dos inimigos Roman.
    /// Faz cache do EnemyCore, Transform próprio, alvo (player) e PlayerHealth.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class RomanEnemyAbilityBase : MonoBehaviour
    {
        [Header("VFX Pooling")]
        [Tooltip("Arraste aqui o prefab 'VFX_Generic_Poolable'")]
        public GameObject vfxGenericPrefab;

        protected EnemyCore enemy;
        protected Transform self;
        protected Transform target;
        protected PlayerHealth playerHealth;

        protected virtual void Awake()
        {
            enemy = GetComponent<EnemyCore>();
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

        /// <summary>
        /// Helper simples: se o player estiver em range, aplica dano directo.
        /// </summary>
        protected void DealDamageToPlayer(float damage, float range)
        {
            if (playerHealth == null || !IsPlayerInRange(range)) return;
            playerHealth.ApplyDamage(damage);
        }

        /// <summary>
        /// Wrapper público para o SpawnVFX.
        /// </summary>
        public GameObject SpawnVFX_Public(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null, float scale = 1f)
        {
            return SpawnVFX(prefab, pos, rot, parent, scale);
        }

        /// <summary>
        /// Spawna um prefab de VFX usando pooling se disponível.
        /// </summary>
        protected GameObject SpawnVFX(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null, float scale = 1f)
        {
            if (prefab == null) return null;

            GameObject vfx = null;
            if (PoolManager.Instance != null)
            {
                vfx = PoolManager.Instance.Spawn(prefab, pos, rot, parent);
            }
            else
            {
                vfx = Instantiate(prefab, pos, rot, parent);
            }

            if (vfx != null && scale != 1f)
            {
                vfx.transform.localScale = Vector3.one * scale;
            }

            return vfx;
        }
    }
}
