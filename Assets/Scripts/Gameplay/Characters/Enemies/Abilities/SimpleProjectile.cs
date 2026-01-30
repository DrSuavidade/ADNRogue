using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    public class SimpleProjectile : MonoBehaviour, IProjectile
    {
        private float _damage;
        private LayerMask _hitMask;
        private float _speed;
        private Vector3 _direction;
        private bool _initialized;

        public void Initialize(float damage, float speed, Vector3 direction, LayerMask mask, float arcHeight = 0f)
        {
            _damage = damage;
            _speed = speed;
            _direction = direction;
            _hitMask = mask;
            _initialized = true;

            // Simple physics if we have a RB, otherwise linear
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = direction * speed;
                rb.useGravity = arcHeight > 0; // Only use gravity if we intended an arc
            }
            // If no RB, Update() handles linear movement
            
            Destroy(gameObject, 5f); // Safety cleanup
        }

        void Update()
        {
            if (!_initialized) return;
            
            transform.position += _direction * _speed * Time.deltaTime;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_initialized) return;
            
            // Check layer mask
            if ((_hitMask.value & (1 << other.gameObject.layer)) == 0) return;

            // Damage player
            // Assuming PlayerHealth is standard, replace with Interface if available
            var playerHealth = other.GetComponent<Geneforge.Gameplay.Characters.Player.PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ApplyDamage(_damage);
            }
            
            Destroy(gameObject);
        }
    }
}
