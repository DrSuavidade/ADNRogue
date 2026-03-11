using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanArcher : RomanEnemyAbilityBase
    {
        [Header("Projectile Settings")]
        public GameObject arrowPrefab;
        public Transform shootOrigin;
        public float arrowSpeed = 15f; 
        public float arcHeight = 1.5f; 
        public float damage = 8f;
        public bool useGravity = true;

        [Header("Fan Shot Settings")]
        [Tooltip("Ângulo lateral entre cada flecha no disparo triplo.")]
        public float fanSpreadAngle = 15f;

        [Header("Visual Correction")]
        [Tooltip("Se a seta voa de lado, mude o Y para 90. Se voa de costas, 180.")]
        public float yawOffset = 90f;

        [Header("Aiming")]
        [Tooltip("Velocidade com que o arqueiro gira para encarar o player.")]
        public float turnSpeed = 10f;
        [Tooltip("Se ativado, o arqueiro vira instantaneamente para o player no momento do tiro.")]
        public bool snapToTargetOnFire = true;

        [Tooltip("Que layers a seta pode atingir.")]
        public LayerMask hitMask = ~0;

        [Header("VFX - Launch (Shockwave)")]
        public GameObject launchVFXPrefab;
        public float launchVFXScale = 1.0f;

        [Header("VFX - Arrow Trail")]
        public GameObject arrowTrailPrefab;
        public float arrowTrailScale = 1.0f;
        [Tooltip("Segundos entre cada spawn de rastro enquanto a flecha voa.")]
        public float trailInterval = 0.08f;

        protected virtual void Update()
        {
            if (enemy != null && enemy.IsDead) return;

            if (target != null)
            {
                Vector3 lookPos = target.position - transform.position;
                lookPos.y = 0; 
                
                if (lookPos.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookPos);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
            }
        }

        public void AnimEvent_ShootArrow()
        {
            PrepareShoot();
            FireArrow(0f);
        }

        public void AnimEvent_ShootTripleArrow()
        {
            PrepareShoot();
            SpawnLaunchVFX();
            FireArrow(-fanSpreadAngle);
            FireArrow(0f);
            FireArrow(fanSpreadAngle);
        }

        private void SpawnLaunchVFX()
        {
            if (launchVFXPrefab == null) return;

            Vector3 spawnPos = transform.position + Vector3.up * 0.05f;
            SpawnVFX(launchVFXPrefab, spawnPos, Quaternion.identity, null, launchVFXScale);
        }

        private void PrepareShoot()
        {
            if (!target) return;

            if (snapToTargetOnFire)
            {
                Vector3 finalLook = target.position - transform.position;
                finalLook.y = 0;
                if (finalLook.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(finalLook);
                }
            }
        }

        private void FireArrow(float lateralAngleOffset)
        {
            if (!arrowPrefab || !shootOrigin || !target) return;

            Vector3 targetPos = target.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetPos - shootOrigin.position;
            
            Vector3 flatDir = toTarget;
            flatDir.y = 0;
            if (flatDir.sqrMagnitude < 0.001f) flatDir = transform.forward;
            
            float baseAngleY = Quaternion.LookRotation(flatDir).eulerAngles.y;
            float finalAngleY = baseAngleY + lateralAngleOffset + yawOffset;
            Quaternion spawnRot = Quaternion.Euler(0, finalAngleY, 0);

            GameObject obj = null;
            if (PoolManager.Instance != null)
                obj = PoolManager.Instance.Spawn(arrowPrefab, shootOrigin.position, spawnRot);
            else
                obj = Instantiate(arrowPrefab, shootOrigin.position, spawnRot);
            
            var proj = obj.GetComponent<RomanArrowProjectile>();
            if (proj == null) proj = obj.AddComponent<RomanArrowProjectile>();
            
            proj.Init(damage, hitMask, finalAngleY, baseAngleY + lateralAngleOffset, this, arrowTrailPrefab, arrowTrailScale, trailInterval);
            
            Rigidbody rb = proj.CachedRigidbody;
            if (rb == null) rb = obj.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.useGravity = useGravity;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                Vector3 velocityDir = Quaternion.Euler(0, lateralAngleOffset, 0) * toTarget.normalized;
                Vector3 velocity = velocityDir * arrowSpeed;
                if (useGravity) velocity.y += arcHeight;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = velocity;
#else
                rb.velocity = velocity;
#endif
            }
        }
    }

    public class RomanArrowProjectile : MonoBehaviour
    {
        private float damage;
        private LayerMask hitMask;
        private float _fixedYaw;
        private float _flightYaw;
        private Rigidbody _rb;
        public Rigidbody CachedRigidbody => _rb;

        private bool _isInitialized;
        private PoolIdentifier _poolId;

        private RomanEnemyAbilityBase _source; 
        private GameObject _trailPrefab;
        private float _trailScale;
        private float _trailInterval;
        private float _trailTimer;

        private void Awake()
        {
            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_poolId == null) _poolId = GetComponent<PoolIdentifier>();
        }

        public void Init(float dmg, LayerMask mask, float yaw, float flightYaw, RomanEnemyAbilityBase source, GameObject trailPrefab, float trailScale, float trailInterval)
        {
            EnsureComponents();
            damage = dmg;
            hitMask = mask;
            _fixedYaw = yaw;
            _flightYaw = flightYaw;
            
            _source = source;
            _trailPrefab = trailPrefab;
            _trailScale = trailScale;
            _trailInterval = trailInterval;

            _isInitialized = true;
            _trailTimer = 0f;
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
            if (PoolManager.Instance != null && _poolId != null)
                PoolManager.Instance.Reclaim(gameObject);
            else if (gameObject.activeInHierarchy)
                Destroy(gameObject);
        }

        void Update()
        {
            if (_trailPrefab == null || _trailInterval <= 0) return;

            _trailTimer += Time.deltaTime;
            if (_trailTimer >= _trailInterval)
            {
                _trailTimer = 0f;
                SpawnTrailSegment();
            }
        }

        private void SpawnTrailSegment()
        {
            if (_source == null || _trailPrefab == null) return;

            GameObject trail = _source.SpawnVFX_Public(
                _trailPrefab, 
                transform.position, 
                Quaternion.Euler(0, _flightYaw + 90f, 0), 
                null, 
                _trailScale
            );
        }

        void FixedUpdate()
        {
            if (!_isInitialized || _rb == null) return;
            _rb.MoveRotation(Quaternion.Euler(0, _fixedYaw, 0));
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_isInitialized) return;

            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            var hp = other.GetComponent<PlayerHealth>();
            if (hp != null)
                hp.ApplyDamage(damage);

            Reclaim();
        }
    }
}

