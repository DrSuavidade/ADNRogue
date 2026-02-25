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
        public Color myColor = Color.white; // Guardamos a cor enviada pelo Painter
        public GameObject puddlePrefab;    // Arraste aqui o Prefab da poça

        [HideInInspector] public Sprite[] puddleFrames;
        [HideInInspector] public float puddleFPS = 10f;
        [HideInInspector] public Vector3 puddleScale = Vector3.one;
        [HideInInspector] public float puddleRotationY = 0f;

        public float visualYawOffset = 90f; // Ajuste no Inspector se a mancha voar de lado

        private float _fixedYaw;
        private Rigidbody _rb;
        private float _spawnTime;
        private bool _isInitialized;
        private bool _hasImpacted;

        public void Init(float dmg, LayerMask mask, float yaw, Color color, bool useGravity)
        {
            damage = dmg;
            hitMask = mask;
            myColor = color;
            _fixedYaw = yaw;
            _hasImpacted = false; 
            _spawnTime = Time.time; // Marca o tempo de nascimento
            
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>(); 

            _rb.isKinematic = false;
            _rb.useGravity = useGravity;
            _rb.constraints = RigidbodyConstraints.FreezeRotation; 
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

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
            // SEGURANÇA: Impede explodir na mão/pé do inimigo segundos após nascer
            if (_hasImpacted || (Time.time - _spawnTime < 0.05f) || other.GetComponentInParent<EnemyCore>() != null) return;

            if (((1 << other.gameObject.layer) & hitMask) != 0)
            {
                _hasImpacted = true; // Marca imediatamente
                var health = other.GetComponentInParent<PlayerHealth>();
                if (health != null) health.ApplyDamage(damage);
                Impact(true);
            }
            else
            {
                // Verifica se não é um Trigger ignorável (como zonas de câmera ou limites invisíveis)
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
                    spawnPos = hit.point + new Vector3(0, 0.02f, 0); 
                }
                else
                {
                    spawnPos = new Vector3(transform.position.x, 0.05f, transform.position.z);
                }

                GameObject puddle = PoolManager.Instance != null
                    ? PoolManager.Instance.Spawn(puddlePrefab, spawnPos, Quaternion.Euler(90, 0, 0))
                    : Instantiate(puddlePrefab, spawnPos, Quaternion.Euler(90, 0, 0));

                var puddleScript = puddle.GetComponent<PaintPuddle>();
                if (puddleScript != null) 
                {
                    puddleScript.Init(myColor, puddleFrames, puddleFPS, puddleScale, puddleRotationY);
                }
            }

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolManager.Instance != null && GetComponent<Geneforge.Core.Pooling.PoolIdentifier>() != null)
                PoolManager.Instance.Reclaim(gameObject);
            else
                Destroy(gameObject);
        }


    }
}
