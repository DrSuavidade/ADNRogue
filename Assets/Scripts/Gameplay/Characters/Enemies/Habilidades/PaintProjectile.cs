using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies;
using Geneforge.Core.Pooling;
using Geneforge.Gameplay.Visuals;

namespace Geneforge.Gameplay.Characters.Enemies.Habilidades
{
    public class PaintProjectile : MonoBehaviour
    {
        public float damage;
        public LayerMask hitMask;
        public Color myColor = Color.white; 
        public GameObject puddlePrefab;    
        public GameObject vfxGenericPrefab; 

        [HideInInspector] public Sprite[] puddleFrames;
        [HideInInspector] public float puddleFPS = 10f;
        [HideInInspector] public float puddleLifetime = 10f;
        [HideInInspector] public Vector3 puddleScale = Vector3.one;
        [HideInInspector] public float puddleRotationY = 0f;

        public float visualYawOffset = 90f; 

        private float _fixedYaw;
        private Rigidbody _rb;
        private float _spawnTime;
        private bool _isInitialized;
        private bool _hasImpacted;
        private PoolIdentifier _poolId;

        private void Awake()
        {
            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_poolId == null) _poolId = GetComponent<PoolIdentifier>();
        }

        public void Init(float dmg, LayerMask mask, float yaw, Color color, bool useGravity, GameObject vfxPrefab)
        {
            EnsureComponents();
            damage = dmg;
            hitMask = mask;
            myColor = color;
            _fixedYaw = yaw;
            vfxGenericPrefab = vfxPrefab;
            _hasImpacted = false; 
            _spawnTime = Time.time; 
            
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = useGravity;
                _rb.constraints = RigidbodyConstraints.FreezeRotation; 
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            _isInitialized = true;
            
            StopAllCoroutines();
            StartCoroutine(LifetimeRoutine(5f));
        }

        private System.Collections.IEnumerator LifetimeRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool();
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || _rb == null) return;
            _rb.MoveRotation(Quaternion.Euler(0, _fixedYaw, 0));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasImpacted || (Time.time - _spawnTime < 0.05f)) return;

            // Use a faster check if possible
            if (((1 << other.gameObject.layer) & hitMask) != 0)
            {
                var health = other.GetComponentInParent<PlayerHealth>();
                if (health != null)
                {
                    _hasImpacted = true; 
                    health.ApplyDamage(damage);
                    Impact(true);
                }
            }
            else
            {
                if (other.isTrigger) return;

                _hasImpacted = true;
                Impact(false);
            }
        }

        private void Impact(bool hitPlayer)
        {
            if (puddlePrefab != null)
            {
                Vector3 rayStart = transform.position + Vector3.up * 1.0f;
                Vector3 spawnPos = transform.position;
                int floorMask = ~((1 << 3) | (1 << 6) | (1 << 2)); 

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f, floorMask, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = hit.point + new Vector3(0, 0.05f, 0); 
                }

                // 1. IMPACT LAYER
                if (PoolManager.Instance != null && vfxGenericPrefab != null)
                {
                    GameObject vfx = PoolManager.Instance.Spawn(vfxGenericPrefab, spawnPos + Vector3.up * 0.2f, Quaternion.identity);
                    var bAnim = vfx.GetComponent<SpriteSheetAnimator>();
                    if (bAnim != null)
                    {
                        bAnim.tintColor = myColor * 2.5f;
                        bAnim.useSpawnScale = true;
                        bAnim.useFadeOut = true;
                        bAnim.scaleMultiplier = Vector3.one * 1.6f;
                        bAnim.loop = false;
                        // Slowed down splash and forced 1.2s duration
                        bAnim.Initialize(puddleFrames, puddleFPS * 0.7f, SpriteSheetAnimator.AnimationMode.Billboard, 1.2f);
                    }
                }

                // 2. POÇA (Floor)
                if (PoolManager.Instance != null)
                {
                    GameObject puddle = PoolManager.Instance.Spawn(puddlePrefab, spawnPos, Quaternion.Euler(90, 0, 0));
                    var puddleScript = puddle.GetComponent<PaintPuddle>();
                    if (puddleScript != null) 
                    {
                        puddleScript.Init(myColor, puddleFrames, puddleFPS, puddleScale, puddleRotationY, 0f, 0f, puddleLifetime);
                    }
                }
            }

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolManager.Instance != null && _poolId != null)
                PoolManager.Instance.Reclaim(gameObject);
            else if (gameObject.activeInHierarchy)
                Destroy(gameObject);
        }
    }
}

