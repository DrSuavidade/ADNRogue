using UnityEngine;
using UnityEngine.Events;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Gameplay.Characters.Enemies.Eras.Present; // para PresentMachineGunProjectile

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Future
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class FutureCyborg : FutureEnemyAbilityBase
    {
        [Header("Referências")]
        [Tooltip("Animator deste inimigo (para trigger e bool de block).")]
        public Animator animator;

        [Header("Cyborg – Rifle de Energia")]
        [Tooltip("Velocidade inicial da bala de energia.")]
        public float bulletSpeed = 45f;

        [Tooltip("Dano por disparo.")]
        public float damagePerShot = 10f;

        [Tooltip("Spread (imprecisão) em graus.")]
        public float spreadAngle = 3f;

        [Tooltip("Tempo de vida da bala (s).")]
        public float bulletLifetime = 5f;

        [Tooltip("Layers atingidas (normalmente Player).")]
        public LayerMask hitMask = ~0;

        // ---------------- Block / Defesa ----------------
        [Header("Block / Defesa")]
        public bool canBlock = true;

        [Tooltip("Percentagem de vida perdida necessária para cada block (0.3 = 30%).")]
        [Range(0.05f, 1f)]
        public float blockHealthLossFraction = 0.3f; // 30%

        [Tooltip("Duração do block (segundos).")]
        public float blockDuration = 5f;

        [Tooltip("Cooldown depois do block acabar (segundos).")]
        public float blockCooldown = 10f;

        [Tooltip("Trigger da animação de block no Animator.")]
        public string blockAnimatorTrigger = "Block";

        [Tooltip("Nome do bool no Animator que indica se está em block.")]
        public string blockBoolName = "IsBlocking";

        [Header("Eventos de VFX / Feedback (opcionais)")]
        public UnityEvent OnBlockStart;
        public UnityEvent OnBlockEnd;

        // ------- estado interno do block -------
        bool _isBlocking = false;
        float _blockCooldownTimer = 0f;
        Coroutine _blockRoutine;

        float _maxHealthCached = 0f;
        float _nextBlockHealthThreshold = 0f;

        // propriedade pública (útil para balas do player verem se está a bloquear)
        public bool IsBlocking => _isBlocking;

        private EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
        }

        void Start()
        {
            // enemy vem do FutureEnemyAbilityBase (EnemyCore)
            if (enemy != null)
            {
                enemy.OnDamaged += HandleOnDamagedForBlock;

                _maxHealthCached = enemy.CurrentHealth;
                if (_maxHealthCached <= 0f)
                    _maxHealthCached = 1f;

                // primeiro limiar de block: perdeu 30% da vida → fica a 70%
                _nextBlockHealthThreshold = _maxHealthCached * (1f - blockHealthLossFraction);
            }
        }

        void OnDestroy()
        {
            if (enemy != null)
                enemy.OnDamaged -= HandleOnDamagedForBlock;
        }

        void Update()
        {
            // --- COOLDOWN DO BLOCK ---
            if (_blockCooldownTimer > 0f)
            {
                _blockCooldownTimer -= Time.deltaTime;
                if (_blockCooldownTimer < 0f)
                    _blockCooldownTimer = 0f;
            }
        }

        // Animation Event na animação de tiro: "AnimEvent_FireGun"
        public void AnimEvent_FireGun()
        {
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
            
            if (_config == null || _config.Archetype == null) return;
            var settings = _config.Archetype.projectile;

            if (!settings.enabled || settings.projectilePrefab == null || !target)
                return;
            
            // Se quiseres, podes impedir disparo enquanto bloqueia:
            // if (_isBlocking) return;
            
            Transform firePoint = transform.Find("ProjectileSpawnPoint");
            if (firePoint == null) firePoint = transform;

            SpawnBullet(firePoint, settings.projectilePrefab);
        }

        void SpawnBullet(Transform firePoint, GameObject bulletPrefab)
        {
            // Direção base para o player
            Vector3 toTarget = target.position - firePoint.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = self.forward;

            Quaternion baseRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            // Spread leve para sentir "rifle futurista"
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

            // Reutiliza o projétil simples que já tens no Presente
            var proj = obj.GetComponent<PresentMachineGunProjectile>();
            if (!proj) proj = obj.AddComponent<PresentMachineGunProjectile>();
            proj.Init(damagePerShot, hitMask, bulletLifetime);
        }

        // =====================================================================
        //                          LÓGICA DO BLOCK
        // =====================================================================

        void HandleOnDamagedForBlock(float dmg)
        {
            if (!canBlock || enemy == null) return;

            // se já está a bloquear ou ainda em cooldown, não tenta bloquear
            if (_isBlocking || _blockCooldownTimer > 0f) return;

            float curHealth = enemy.CurrentHealth;

            // se por algum motivo ainda não tínhamos max health, recalculamos
            if (_maxHealthCached <= 0f)
                _maxHealthCached = Mathf.Max(curHealth + dmg, 1f);

            // se a vida atual passou para baixo do limiar → ativa block
            if (curHealth <= _nextBlockHealthThreshold)
            {
                StartBlock();

                // próximo limiar: menos mais 30% da vida máxima
                _nextBlockHealthThreshold -= _maxHealthCached * blockHealthLossFraction;
                if (_nextBlockHealthThreshold < 0f)
                    _nextBlockHealthThreshold = 0f;
            }
        }

        void StartBlock()
        {
            if (_blockRoutine != null) return;

            _isBlocking = true;
            _blockCooldownTimer = blockCooldown;

            // avisa o Animator que está em block e dispara trigger
            if (animator)
            {
                if (!string.IsNullOrEmpty(blockBoolName))
                    animator.SetBool(blockBoolName, true);

                if (!string.IsNullOrEmpty(blockAnimatorTrigger))
                    animator.SetTrigger(blockAnimatorTrigger);
            }

            // evento de início de block (shield, som, etc)
            OnBlockStart?.Invoke();

            _blockRoutine = StartCoroutine(BlockRoutine());
        }

        System.Collections.IEnumerator BlockRoutine()
        {
            float t = 0f;
            while (t < blockDuration)
            {
                t += Time.deltaTime;
                yield return null;
            }

            _isBlocking = false;

            if (animator && !string.IsNullOrEmpty(blockBoolName))
                animator.SetBool(blockBoolName, false);

            // evento de fim de block
            OnBlockEnd?.Invoke();

            _blockRoutine = null;
        }
    }
}
