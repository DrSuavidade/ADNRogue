using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Gameplay.Characters.Enemies.Eras.Present; // PresentMachineGunProjectile

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Future
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class FutureSniper : FutureEnemyAbilityBase
    {
        [Header("Sniper – Railgun")]
        [Tooltip("Velocidade inicial da bala de sniper.")]
        public float bulletSpeed = 80f;

        [Tooltip("Dano por disparo.")]
        public float damagePerShot = 25f;

        [Tooltip("Spread (imprecisão) em graus – muito baixo para sniper.")]
        public float spreadAngle = 0.3f;

        [Tooltip("Tempo de vida da bala (s).")]
        public float bulletLifetime = 6f;

        [Tooltip("Layers atingidas (normalmente Player).")]
        public LayerMask hitMask = ~0;

        private EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
        }

        // Animation Event na animação de sniper: "AnimEvent_SniperShot"
        public void AnimEvent_SniperShot()
        {
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();

            if (_config == null || _config.Archetype == null) return;
            var settings = _config.Archetype.projectile;
            
            if (!settings.enabled || settings.projectilePrefab == null || !target)
                return;

            Transform spawnPoint = transform.Find("ProjectileSpawnPoint");
            if (spawnPoint == null) spawnPoint = transform;

            SpawnSniperBullet(spawnPoint, settings.projectilePrefab);
        }

        void SpawnSniperBullet(Transform firePoint, GameObject bulletPrefab)
        {
            Vector3 toTarget = target.position - firePoint.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            Quaternion baseRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            // Quase sem spread – só um bocadinho para não ser 100% perfeito
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
