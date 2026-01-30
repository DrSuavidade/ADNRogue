using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Present
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class PresentBomber : PresentEnemyAbilityBase
    {
        [Header("Grenade Throw")]
        [Tooltip("Velocidade horizontal base da granada.")]
        public float throwSpeed = 16f;

        [Tooltip("Bónus vertical aplicado à velocidade para fazer o arco.")]
        public float arcHeight = 2.0f;

        [Tooltip("Tempo máximo antes de explodir automaticamente.")]
        public float grenadeLifetime = 3.5f;

        [Header("Explosão")]
        public float explosionRadius = 2.5f;
        public float explosionDamage = 12f;
        public LayerMask hitMask = ~0;

        EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
        }

        // Animation Event: AnimEvent_ThrowGrenade
        public void AnimEvent_ThrowGrenade()
        {
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
            
            if (_config == null || _config.Archetype == null) return;
            var settings = _config.Archetype.projectile;

            if (!settings.enabled || settings.projectilePrefab == null || !target)
                return;

            Transform origin = transform.Find("ProjectileSpawnPoint");
            if (origin == null) origin = transform;
            
            GameObject prefab = settings.projectilePrefab;

            var obj = Object.Instantiate(prefab, origin.position, origin.rotation);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 toTarget = target.position - origin.position;

                Vector3 flat = toTarget;
                flat.y = 0f;
                if (flat.sqrMagnitude < 0.0001f)
                    flat = self.forward;

                flat.Normalize();
                Vector3 vel = flat * throwSpeed;
                vel.y += settings.arcHeight; // Load from config

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = vel;
#else
                rb.velocity = vel;
#endif
            }

            var grenade = obj.GetComponent<PresentGrenadeProjectile>();
            if (!grenade) grenade = obj.AddComponent<PresentGrenadeProjectile>();
            grenade.Init(explosionDamage, explosionRadius, hitMask, grenadeLifetime);
        }
    }

    public class PresentGrenadeProjectile : MonoBehaviour
    {
        float damage;
        float radius;
        LayerMask hitMask;
        float remainingLife;
        bool exploded;

        public void Init(float dmg, float rad, LayerMask mask, float lifeTime)
        {
            damage = dmg;
            radius = rad;
            hitMask = mask;
            remainingLife = lifeTime;
        }

        void Update()
        {
            if (exploded) return;

            remainingLife -= Time.deltaTime;
            if (remainingLife <= 0f)
            {
                Explode();
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (exploded) return;
            Explode();
        }

        void OnTriggerEnter(Collider other)
        {
            if (exploded) return;

            // Se bater em algo com layer válida, explode
            if ((hitMask.value & (1 << other.gameObject.layer)) != 0)
            {
                Explode();
            }
        }

        void Explode()
        {
            exploded = true;

            Collider[] hits = Physics.OverlapSphere(transform.position, radius, hitMask);
            foreach (var col in hits)
            {
                var hp = col.GetComponentInParent<PlayerHealth>();
                if (hp != null)
                    hp.ApplyDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}