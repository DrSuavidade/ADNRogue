using UnityEngine;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    public class BossProjectile : MonoBehaviour
    {
        private float damage;
        private LayerMask hitMask;
        private bool isInitialized;

        [Header("Homing")]
        private bool isHoming;
        private float homingStrength;
        private float speed;
        private Transform target;
        private Rigidbody rb;

        public void Init(float dmg, LayerMask mask, bool homing = false, float strength = 5f, float projSpeed = 0f, Transform homingTarget = null)
        {
            damage = dmg;
            hitMask = mask;
            isHoming = homing;
            homingStrength = strength;
            speed = projSpeed;
            target = homingTarget;
            rb = GetComponent<Rigidbody>();
            isInitialized = true;
            
            // Force triggers so they don't bounce off physical Player Bullets
            Collider[] cols = GetComponentsInChildren<Collider>();
            if (cols != null)
            {
                foreach (var c in cols) c.isTrigger = true;
            }

            // Failsafe cleanup (just in case they don't hit a wall)
            Destroy(gameObject, 6f);
        }

        void FixedUpdate()
        {
            if (!isInitialized || !isHoming || target == null || rb == null) return;

            Vector3 direction = (target.position - transform.position).normalized;
            
            // Anti-Orbiting: If the target is behind the projectile, disable homing
            // This prevents the projectile from circling back around
            float dot = Vector3.Dot(transform.forward, direction);
            if (dot < 0)
            {
                isHoming = false;
                return;
            }

            Vector3 lookDir = direction;
            lookDir.y = 0; // Keep it mostly horizontal for this game's feel
            
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, homingStrength * Time.fixedDeltaTime));
            }

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = transform.forward * speed;
#else
            rb.velocity = transform.forward * speed;
#endif
        }

        void OnTriggerEnter(Collider other)
        {
            if (!isInitialized) return;

            // Optional: skip collisions with other enemies/boss parts if desired
            if (other.gameObject.layer == gameObject.layer) return;

            // Ignore Player Bullets to prevent intercepting
            if (other.GetComponent<Geneforge.Gameplay.Weapons.Bullets.Bullet>() != null) return;

            var hp = other.GetComponent<PlayerHealth>();
            if (hp != null)
            {
                hp.ApplyDamage(damage);
                Destroy(gameObject);
                return;
            }

            // Hit environment (walls, obstacles)
            if (!other.isTrigger && ((hitMask.value & (1 << other.gameObject.layer)) != 0))
            {
                Destroy(gameObject);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            // Fallback just in case user uses non-trigger Colliders on projectile
            OnTriggerEnter(collision.collider);
        }
    }
}
