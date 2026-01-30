using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Enemies.Config;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Present
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class PresentGunGuy : PresentEnemyAbilityBase
    {
        [Header("Gun")]
        [Tooltip("Velocidade inicial da bala.")]
        public float bulletSpeed = 30f;

        [Tooltip("Dano por disparo.")]
        public float damagePerShot = 8f;

        [Tooltip("Spread (imprecisão) em graus.")]
        public float spreadAngle = 2f;

        [Tooltip("Tempo de vida da bala.")]
        public float bulletLifetime = 4f;

        [Tooltip("Layers atingidas (normalmente Player).")]
        public LayerMask hitMask = ~0;

        EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
        }

        // Animation Event: AnimEvent_FireGun
        public void AnimEvent_FireGun()
        {
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
            
            if (_config == null || _config.Archetype == null) return;
            var settings = _config.Archetype.projectile;

            if (!settings.enabled || settings.projectilePrefab == null || !target)
                return;
            
            Transform firePoint = transform.Find("ProjectileSpawnPoint");
            if (firePoint == null) firePoint = transform;

            SpawnBullet(firePoint, settings.projectilePrefab);
        }

        void SpawnBullet(Transform firePoint, GameObject bulletPrefab)
        {
            Vector3 toTarget = target.position - firePoint.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            Quaternion baseRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            float yaw   = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
            float pitch = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
            Quaternion spreadRot = baseRot * Quaternion.Euler(pitch, yaw, 0f);

            var obj = Object.Instantiate(bulletPrefab, firePoint.position, spreadRot);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 vel = spreadRot * Vector3.forward * bulletSpeed;
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = vel;
#else
                rb.velocity = vel;
#endif
            }

            var proj = obj.GetComponent<PresentMachineGunProjectile>();
            if (!proj) proj = obj.AddComponent<PresentMachineGunProjectile>();
            proj.Init(damagePerShot, hitMask, bulletLifetime);
        }
    }
}
