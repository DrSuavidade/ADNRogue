using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Weapons.Bullets
{
    public class BulletSimple : MonoBehaviour
    {
        [Header("Configuração")]
        [SerializeField] private float speed = 25f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float lifeTime = 3f;

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        public float Damage
        {
            get => damage;
            set => damage = value;
        }

        public float LifeTime
        {
            get => lifeTime;
            set => lifeTime = value;
        }


        [Header("Referências")]
        private Rigidbody rb;
        PoolIdentifier poolId;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            poolId = GetComponent<PoolIdentifier>();

            if (rb == null)
            {
                Debug.LogError("A bala precisa de um Rigidbody!");
                return;
            }

            rb.useGravity = false;
            rb.isKinematic = false;
        }

        void OnEnable()
        {
            if (rb == null) return;

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = transform.forward * speed;
            rb.angularVelocity = Vector3.zero;
#else
            rb.velocity = transform.forward * speed;
            rb.angularVelocity = Vector3.zero;
#endif
            CancelInvoke(nameof(Despawn));
            Invoke(nameof(Despawn), lifeTime);
        }


        void Despawn()
        {
            CancelInvoke(nameof(Despawn));

            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                rb.angularVelocity = Vector3.zero;
            }

            if (poolId != null && PoolManager.Instance != null)
                PoolManager.Instance.Reclaim(gameObject);
            else
                Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            PlayerHealth ph = other.GetComponent<PlayerHealth>();

            if (ph != null)
            {
                ph.ApplyDamage(damage);
                Despawn();
                return;
            }

            if (!other.isTrigger)
                Despawn();
        }
    }
}
