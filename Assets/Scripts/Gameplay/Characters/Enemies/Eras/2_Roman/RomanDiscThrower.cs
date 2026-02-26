using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies.Config;
using Geneforge.Core.Pooling;

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

        [Header("VFX da Aura")]
        public Sprite[] auraFrames;
        public float auraFPS = 6f;
        public Vector3 auraScale = Vector3.one;
        [ColorUsage(true, true)] public Color auraColor = Color.white;

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

        private GameObject _activeAura;

        public void Init(float dmg, LayerMask mask, float rotSpeed, RomanDiscThrower owner)
        {
            damage = dmg;
            hitMask = mask;
            rotationSpeed = rotSpeed;
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

            // --- LIMPEZA DE AURAS ANTIGAS (POOLING SAFETY) ---
            if (_activeAura != null)
            {
                if (PoolManager.Instance != null) PoolManager.Instance.Reclaim(_activeAura);
                else Destroy(_activeAura);
                _activeAura = null;
            }

            // --- SPAWN DA AURA ---
            if (owner != null && owner.auraFrames != null && owner.auraFrames.Length > 0)
            {
                // Criamos o VFX como FILHO do projétil para ele seguir o movimento
                _activeAura = owner.SpawnVFXLayer_Public(
                    "DiscGlow", 
                    transform.position, 
                    owner.auraScale, 
                    owner.auraFrames, 
                    owner.auraFPS, 
                    owner.auraColor, 
                    1f, 0f, 0f, true, transform, 
                    Visuals.SpriteSheetAnimator.AnimationMode.Billboard, 
                    true // Loop = true
                );

                if (_activeAura != null)
                {
                    _activeAura.transform.localPosition = Vector3.zero;
                }
            }

            _isInitialized = true;
            StopAllCoroutines();
            StartCoroutine(AutoReclaim(6f));
        }

        private System.Collections.IEnumerator AutoReclaim(float delay)
        {
            yield return new WaitForSeconds(delay);
            Reclaim();
        }

        private void Reclaim()
        {
            // Reclamamos a aura também para não vazar memória/objetos
            if (_activeAura != null)
            {
                if (PoolManager.Instance != null) PoolManager.Instance.Reclaim(_activeAura);
                else Destroy(_activeAura);
                _activeAura = null;
            }

            if (PoolManager.Instance != null)
                PoolManager.Instance.Reclaim(gameObject);
            else
                Destroy(gameObject);
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


