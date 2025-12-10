using UnityEngine;
using UnityEngine.Events;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Gameplay.Characters.Enemies.Eras.Present; // PresentMachineGunProjectile

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Future
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class FuturePsychic : FutureEnemyAbilityBase
    {
        [Header("Referências")]
        [Tooltip("Animator deste inimigo (para triggers de ataque).")]
        public Animator animator;

        [Tooltip("Transform de onde sai o laser da testa.")]
        public Transform headFirePoint;

        [Tooltip("Transform de onde sai o laser do 2º ataque (braços juntos).")]
        public Transform armsFirePoint;   // UM origin chega :)

        [Header("Laser / Projectile")]
        [Tooltip("Prefab do laser/projétil (usa o mesmo sistema do presente).")]
        public GameObject laserPrefab;

        [Tooltip("Velocidade do laser.")]
        public float laserSpeed = 60f;

        [Tooltip("Dano por laser.")]
        public float laserDamage = 15f;

        [Tooltip("Tempo de vida do laser (s).")]
        public float laserLifetime = 4f;

        [Tooltip("Layers que o laser pode atingir.")]
        public LayerMask hitMask = ~0;

        [Header("Eventos de VFX (opcionais)")]
        public UnityEvent OnHeadLaserFired;
        public UnityEvent OnArmsLaserFired;

        private EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
        }

        // --------------------------------------------------------------------
        //  ANIMATION EVENTS
        //  (liga estes métodos nas animações do Psychic)
        // --------------------------------------------------------------------

        /// <summary>
        /// Chamado por Animation Event no ataque em que dispara da testa.
        /// </summary>
        public void AnimEvent_FireHeadLaser()
        {
            FireLaser(headFirePoint);
            OnHeadLaserFired?.Invoke();
        }

        /// <summary>
        /// Chamado por Animation Event no 2º ataque (braços juntos).
        /// </summary>
        public void AnimEvent_FireArmsLaser()
        {
            FireLaser(armsFirePoint);
            OnArmsLaserFired?.Invoke();
        }

        // --------------------------------------------------------------------
        //  LÓGICA DE DISPARO
        // --------------------------------------------------------------------

        private void FireLaser(Transform firePoint)
        {
            if (!firePoint || !laserPrefab || !target)
                return;

            // Direção para o alvo
            Vector3 toTarget = target.position - firePoint.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            // opcional: mantemos laser mais "horizontal"
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            Quaternion rot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            // Instanciar projétil
            GameObject obj = Object.Instantiate(laserPrefab, firePoint.position, rot);

            // Dar velocidade
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 vel = rot * Vector3.forward * laserSpeed;
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = vel;
#else
                rb.velocity = vel;
#endif
            }

            // Usar o mesmo componente de projétil que já tens no Presente
            var proj = obj.GetComponent<PresentMachineGunProjectile>();
            if (!proj)
                proj = obj.AddComponent<PresentMachineGunProjectile>();

            proj.Init(laserDamage, hitMask, laserLifetime);
        }
    }
}
