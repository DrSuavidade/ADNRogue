using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies.Config; // para EnemyConfigurator

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Present
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class PresentMachineGun : PresentEnemyAbilityBase
    {
        [Header("Machine Gun")]
        [Tooltip("Velocidade inicial da bala.")]
        public float bulletSpeed = 40f;

        [Tooltip("Número de balas por rajada.")]
        public int bulletsPerBurst = 5;

        [Tooltip("Ângulo de spread (graus) horizontal/vertical.")]
        public float spreadAngle = 6f;

        [Header("Dano")]
        public float damagePerBullet = 3f;

        [Tooltip("Layers que podem ser atingidos (normalmente só o Player).")]
        public LayerMask hitMask = ~0;

        [Tooltip("Tempo de vida máximo da bala (segundos).")]
        public float bulletLifetime = 3f;

        EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
        }

        // Chamado por Animation Event no clip de ataque: AnimEvent_MG_Burst
        public void AnimEvent_MG_Burst()
        {
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
            
            if (_config == null || _config.Archetype == null) return;
            var settings = _config.Archetype.projectile;

            if (!settings.enabled || settings.projectilePrefab == null || !target)
                return;
            
            Transform firePoint = transform.Find("ProjectileSpawnPoint");
            if (firePoint == null) firePoint = transform;

            for (int i = 0; i < bulletsPerBurst; i++)
            {
                SpawnBulletWithSpread(firePoint, settings.projectilePrefab);
            }
        }

        void SpawnBulletWithSpread(Transform firePoint, GameObject bulletPrefab)
        {
            // Direção base para o player
            Vector3 toTarget = target.position - firePoint.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            Quaternion baseRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            // Pequeno spread aleatório
            float yaw   = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
            float pitch = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
            Quaternion spreadRot = baseRot * Quaternion.Euler(pitch, yaw, 0f);

            var obj = Object.Instantiate(bulletPrefab, firePoint.position, spreadRot);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = spreadRot * Vector3.forward * bulletSpeed;
#else
                rb.velocity = spreadRot * Vector3.forward * bulletSpeed;
#endif
            }

            var proj = obj.GetComponent<PresentMachineGunProjectile>();
            if (!proj) proj = obj.AddComponent<PresentMachineGunProjectile>();
            proj.Init(damagePerBullet, hitMask, bulletLifetime);
        }
    }

    /// <summary>
    /// Projétil simples do MachineGun: dá dano ao Player e morre.
    /// </summary>
    public class PresentMachineGunProjectile : MonoBehaviour
    {
        float damage;
        LayerMask hitMask;

        public void Init(float dmg, LayerMask mask, float lifeTime)
        {
            damage  = dmg;
            hitMask = mask;
            Destroy(gameObject, lifeTime);
        }

        void OnTriggerEnter(Collider other)
        {
            // 1) filtrar pela Layer
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            // 2) procurar PlayerHealth no PAI (collider pode ser filho)
            var hp = other.GetComponentInParent<Player.PlayerHealth>();
            if (hp != null)
            {
                hp.ApplyDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
