using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Core.Pooling;
using System.Collections;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class RomanDiscThrower : RomanEnemyAbilityBase
    {
        [Header("Tiro")]
        public float throwSpeed = 15f;
        public float arcHeight = 0.5f; 
        public float damage = 12f;
        public float discRotationSpeed = 1500f; 

        [Header("VFX Prefabs")]
        public GameObject auraPrefab;
        public float auraScaleMult = 1.0f;
        public GameObject trailPrefab;
        public float trailScaleMult = 1.0f;
        public float trailInterval = 0.05f;

        [Tooltip("Layers que o disco pode atingir.")]
        public LayerMask hitMask = ~0;

        EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
        }

        public void AnimEvent_ThrowDisc()
        {
            ThrowSingleDisc(0f);
        }

        public void AnimEvent_ThrowDiscVolley()
        {
            ThrowSingleDisc(0f);    
            ThrowSingleDisc(-18f);  
            ThrowSingleDisc(18f);   
        }

        private void ThrowSingleDisc(float angleOffset)
        {
            if (_config == null) _config = GetComponent<EnemyConfigurator>();
            if (_config == null || _config.Archetype == null) return;
            
            var settings = _config.Archetype.projectile;
            if (!settings.enabled || settings.projectilePrefab == null || !target) return;

            Transform origin = transform.Find("ProjectileSpawnPoint");
            if (origin == null) origin = transform;
            
            Vector3 targetCenter = target.position + Vector3.up * 1.2f;
            Vector3 to = targetCenter - origin.position;
            
            Vector3 flatDir = to;
            flatDir.y = 0;
            if (flatDir.sqrMagnitude < 0.0001f) flatDir = transform.forward;
            
            // --- CORREÇÃO DE ORIENTAÇÃO ---
            Vector3 prefabEuler = settings.projectilePrefab.transform.eulerAngles;
            float targetYaw = Quaternion.LookRotation(flatDir).eulerAngles.y + angleOffset;
            Quaternion spawnRot = Quaternion.Euler(prefabEuler.x, targetYaw, prefabEuler.z);

            GameObject obj = null;
            if (PoolManager.Instance != null)
                obj = PoolManager.Instance.Spawn(settings.projectilePrefab, origin.position, spawnRot);
            else
                obj = Instantiate(settings.projectilePrefab, origin.position, spawnRot);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.useGravity = true;
                Vector3 shootDir = Quaternion.Euler(0, angleOffset, 0) * to.normalized;
                Vector3 vel = shootDir * throwSpeed;
                vel.y += arcHeight;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = vel;
#else
                rb.velocity = vel;
#endif
            }

            var proj = obj.GetComponent<RomanDiscProjectile>();
            if (!proj) proj = obj.AddComponent<RomanDiscProjectile>();
            
            // Passamos as referências do VFX para o projétil
            proj.Init(damage, hitMask, discRotationSpeed, this);
        }
    }

    public class RomanDiscProjectile : MonoBehaviour
    {
        private float damage;
        private LayerMask hitMask;
        private float rotationSpeed;
        private float _currentYaw;
        private float _fixedX;
        private float _fixedZ;
        private Rigidbody _rb;
        private bool _isInitialized;
        private RomanDiscThrower _owner;

        private GameObject _activeAura;
        private Coroutine _trailCoroutine;
        private WaitForSeconds _trailWait;

        public void Init(float dmg, LayerMask mask, float rotSpeed, RomanDiscThrower owner)
        {
            damage = dmg;
            hitMask = mask;
            rotationSpeed = rotSpeed;
            _owner = owner;
            _rb = GetComponent<Rigidbody>();
            
            _fixedX = transform.eulerAngles.x;
            _fixedZ = transform.eulerAngles.z;
            _currentYaw = transform.eulerAngles.y;
            
            if (_rb != null)
            {
                _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // --- LIMPEZA DE AURAS E RASTROS ANTIGOS (POOLING SAFETY) ---
            CleanupActiveVFX();

            // --- SPAWN DA AURA ---
            if (owner != null && owner.auraPrefab != null)
            {
                _activeAura = owner.SpawnVFX_Public(
                    owner.auraPrefab, 
                    transform.position, 
                    Quaternion.identity, 
                    transform, 
                    owner.auraScaleMult
                );

                if (_activeAura != null)
                {
                    var anim = _activeAura.GetComponent<Geneforge.Gameplay.Visuals.SpriteSheetAnimator>();
                    if (anim != null) anim.loop = true;
                }
            }

            // --- INICIAR RASTRO ---
            if (owner != null && owner.trailPrefab != null)
            {
                _trailWait = new WaitForSeconds(_owner.trailInterval);
                _trailCoroutine = StartCoroutine(TrailRoutine());
            }

            _isInitialized = true;
            StopCoroutine(nameof(AutoReclaim));
            StartCoroutine(nameof(AutoReclaim), 6f);
        }

        private IEnumerator TrailRoutine()
        {
            while (true)
            {
                yield return _trailWait; // REUTILIZAÇÃO: Zero alocação de lixo aqui
                
                // Spawna o rastro no ESPAÇO DO MUNDO (parent = null)
                // Usamos Loop = false para ele tocar a animação uma vez e sumir (pooling automático)
                _owner.SpawnVFX_Public(
                    _owner.trailPrefab,
                    transform.position,
                    Quaternion.Euler(0, Random.Range(0, 360f), 0),
                    null, 
                    _owner.trailScaleMult
                );
            }
        }

        private IEnumerator AutoReclaim(float delay)
        {
            yield return new WaitForSeconds(delay);
            Reclaim();
        }

        private void Reclaim()
        {
            CleanupActiveVFX();

            if (PoolManager.Instance != null)
                PoolManager.Instance.Reclaim(gameObject);
            else
                Destroy(gameObject);
        }

        private void CleanupActiveVFX()
        {
            if (_trailCoroutine != null)
            {
                StopCoroutine(_trailCoroutine);
                _trailCoroutine = null;
            }

            if (_activeAura != null)
            {
                if (PoolManager.Instance != null) PoolManager.Instance.Reclaim(_activeAura);
                else Destroy(_activeAura);
                _activeAura = null;
            }
        }

        void FixedUpdate()
        {
            if (!_isInitialized || _rb == null) return;

            _currentYaw += rotationSpeed * Time.fixedDeltaTime;
            _rb.MoveRotation(Quaternion.Euler(_fixedX, _currentYaw, _fixedZ));

            Vector3 hVel = _rb.linearVelocity;
            hVel.y = 0;
            float horizontalSpeed = hVel.magnitude;

            if (horizontalSpeed > 1f)
            {
                float liftStrength = Mathf.Min(horizontalSpeed * 0.35f, 8.5f);
                _rb.AddForce(Vector3.up * liftStrength, ForceMode.Acceleration);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_isInitialized) return;

            var hp = other.GetComponent<PlayerHealth>();
            if (hp != null)
            {
                hp.ApplyDamage(damage);
                Reclaim();
                return;
            }

            if (!other.isTrigger && ((hitMask.value & (1 << other.gameObject.layer)) != 0))
            {
                Reclaim();
            }
        }
    }
}


