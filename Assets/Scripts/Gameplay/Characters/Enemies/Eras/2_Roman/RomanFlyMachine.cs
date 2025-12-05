using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
///Develop
namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanFlyMachine : RomanEnemyAbilityBase
    {
        [Header("Bombs")]
        [Tooltip("Prefab da bomba que a máquina larga.")]
        public GameObject bombPrefab;

        [Tooltip("Ponto de onde a bomba é largada (por ex. um child em baixo da máquina).")]
        public Transform dropPoint;

        [Tooltip("Velocidade inicial para baixo da bomba.")]
        public float dropSpeed = 5f;

        [Tooltip("Dano da explosão da bomba.")]
        public float bombDamage = 10f;

        [Tooltip("Raio da explosão da bomba.")]
        public float bombRadius = 2.5f;

        [Tooltip("Layers afectados pelo raio da bomba (normalmente Player).")]
        public LayerMask hitMask = ~0;

        /// <summary>
        /// Evento de animação quando a máquina larga a bomba
        /// (ex: 'AnimEvent_DropBomb').
        /// </summary>
        public void AnimEvent_DropBomb()
        {
            if (!bombPrefab) return;

            Vector3 pos = dropPoint ? dropPoint.position : transform.position;

            var obj = Instantiate(bombPrefab, pos, Quaternion.identity);
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Larga a bomba com uma velocidade inicial para baixo
                Vector3 vel = Vector3.down * dropSpeed;
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = vel;
#else
                rb.velocity = vel;
#endif
            }

            var proj = obj.GetComponent<RomanFlyMachineBombProjectile>();
            if (!proj) proj = obj.AddComponent<RomanFlyMachineBombProjectile>();
            proj.Init(bombDamage, bombRadius, hitMask);
        }
    }

    /// <summary>
    /// Lógica da bomba: cai, ao colidir explode num raio à volta.
    /// </summary>
    public class RomanFlyMachineBombProjectile : MonoBehaviour
    {
        float damage;
        float radius;
        LayerMask hitMask;

        public void Init(float dmg, float r, LayerMask mask)
        {
            damage = dmg;
            radius = r;
            hitMask = mask;
            // Segurança: destruir se nunca bater em nada
            Destroy(gameObject, 8f);
        }

        void OnCollisionEnter(Collision collision)
        {
            Explode();
        }

        void OnTriggerEnter(Collider other)
        {
            // Caso uses Trigger em vez de collider normal
            Explode();
        }

        void Explode()
        {
            // Evitar explodir mais que uma vez
            if (!gameObject.activeInHierarchy) return;

            var cols = Physics.OverlapSphere(transform.position, radius, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var hp = cols[i].GetComponentInParent<PlayerHealth>();
                if (hp != null)
                    hp.ApplyDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}
