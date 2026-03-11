using UnityEngine;
using Geneforge.Core.Pooling;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    /// <summary>
    /// Component for the individual orbs that can either orbit a boss or be launched as a projectile.
    /// Uses Pooling for efficiency.
    /// </summary>
    public class BossOrb : MonoBehaviour
    {
        private enum OrbState { Orbiting, Attacking }
        private OrbState _currentState = OrbState.Orbiting;

        private float _damage;
        private LayerMask _hitMask;
        private Transform _target;
        private float _speed;
        private bool _isHoming;
        private float _homingStrength;
        private Rigidbody _rb;
        private bool _isInitialized;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
                _rb.useGravity = false;
                _rb.isKinematic = true; 
            }
        }

        /// <summary>
        /// Resets the orb to its orbiting state.
        /// </summary>
        public void SetOrbiting()
        {
            _currentState = OrbState.Orbiting;
            _isInitialized = false;
            if (_rb) _rb.isKinematic = true;
            
            // Disable trail if it has one
            var trail = GetComponentInChildren<TrailRenderer>();
            if (trail != null) trail.emitting = false;
        }

        /// <summary>
        /// Launches the orb towards a target.
        /// </summary>
        public void Launch(float damage, LayerMask hitMask, Transform target, float speed, bool homing, float homingStrength)
        {
            _damage = damage;
            _hitMask = hitMask;
            _target = target;
            _speed = speed;
            _isHoming = homing;
            _homingStrength = homingStrength;
            
            _currentState = OrbState.Attacking;
            if (_rb)
            {
                _rb.isKinematic = false;
                _rb.useGravity = false;
                
                // Initial direction
                Vector3 dir = (target.position - transform.position).normalized;
                dir.y = 0; // Keep it mostly horizontal
                if (dir.sqrMagnitude < 0.1f) dir = transform.forward;
                
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity = dir * _speed;
#else
                _rb.velocity = dir * _speed;
#endif
            }
            
            _isInitialized = true;

            // Enable trail
            var trail = GetComponentInChildren<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }

            // Failsafe reclaim after 5 seconds
            CancelInvoke(nameof(Reclaim));
            Invoke(nameof(Reclaim), 5f);
        }

        private void FixedUpdate()
        {
            if (_currentState != OrbState.Attacking || !_isInitialized || _rb == null) return;

#if UNITY_6000_0_OR_NEWER
            Vector3 currentVel = _rb.linearVelocity;
#else
            Vector3 currentVel = _rb.velocity;
#endif
            Vector3 currentDir = currentVel.normalized;
            if (currentDir.sqrMagnitude < 0.1f) currentDir = transform.forward;

            if (_isHoming && _target != null)
            {
                Vector3 targetDir = (_target.position - transform.position).normalized;
                float dot = Vector3.Dot(currentDir, targetDir);
                
                // Only home if target is roughly in front to avoid orbiting behavior around player
                if (dot > 0)
                {
                    currentDir = Vector3.Slerp(currentDir, targetDir, _homingStrength * Time.fixedDeltaTime);
                }
            }

#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = currentDir * _speed;
#else
            _rb.velocity = currentDir * _speed;
#endif
            if (currentDir.sqrMagnitude > 0.001f)
                _rb.MoveRotation(Quaternion.LookRotation(currentDir));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_currentState != OrbState.Attacking) return;

            // Don't hit other enemies or boss parts
            if (other.gameObject.layer == gameObject.layer) return;

            var player = other.GetComponent<PlayerHealth>();
            if (player != null)
            {
                player.ApplyDamage(_damage);
                Reclaim();
                return;
            }

            // Environment hit
            if (!other.isTrigger && ((_hitMask.value & (1 << other.gameObject.layer)) != 0))
            {
                Reclaim();
            }
        }

        private void Reclaim()
        {
            CancelInvoke(nameof(Reclaim));
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Reclaim(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
