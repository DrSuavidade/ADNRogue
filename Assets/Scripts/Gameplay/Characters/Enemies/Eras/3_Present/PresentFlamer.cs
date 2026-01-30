using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Present
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class PresentFlamer : PresentEnemyAbilityBase
    {
        [Header("Flamethrower")]
        [Tooltip("Distância máxima do jacto de fogo.")]
        public float flameRange = 5f;

        [Tooltip("Ângulo total do cone de fogo (graus).")]
        public float flameAngle = 40f;

        [Tooltip("Dano por segundo dentro do cone.")]
        public float damagePerSecond = 15f;

        [Tooltip("Layers atingidas (normalmente Player).")]
        public LayerMask hitMask = ~0;

        EnemyConfigurator _config;
        Transform flameOrigin;
        bool isFiring;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();

            Transform spawnPoint = null;
            if (_config != null && _config.Archetype != null && _config.Archetype.projectile.enabled)
            {
                // Configurator creates "ProjectileSpawnPoint"
                spawnPoint = transform.Find("ProjectileSpawnPoint");
            }
            
            flameOrigin = spawnPoint != null ? spawnPoint : transform;
        }

        void Update()
        {
            if (!isFiring || playerHealth == null || !target)
                return;

            ApplyFlameDamage(Time.deltaTime);
        }

        // Animation Event no início da rajada de fogo
        public void AnimEvent_Flame_Start()
        {
            isFiring = true;
        }

        // Animation Event no fim da rajada
        public void AnimEvent_Flame_Stop()
        {
            isFiring = false;
        }

        void ApplyFlameDamage(float deltaTime)
        {
            Vector3 origin = flameOrigin.position;
            Vector3 forward = flameOrigin.forward;
            Vector3 toTarget = target.position - origin;

            float distance = toTarget.magnitude;
            if (distance > flameRange)
                return;

            toTarget.Normalize();
            float angle = Vector3.Angle(forward, toTarget);
            if (angle > flameAngle * 0.5f)
                return;

            if ((hitMask.value & (1 << target.gameObject.layer)) == 0)
                return;

            float damage = damagePerSecond * deltaTime;
            playerHealth.ApplyDamage(damage);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!flameOrigin) flameOrigin = transform;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(flameOrigin.position, flameRange);
        }
#endif
    }
}
