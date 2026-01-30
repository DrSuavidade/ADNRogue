using UnityEngine;
using Geneforge.Core.Pooling;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    /// <summary>
    /// Generic ability to spawn a projectile from an animation event.
    /// Replaces specific scripts like PrehistoricFireThrower.
    /// Configure the prefab and spawn point in the component or via EnemyArchetype.
    /// </summary>
    public class ProjectileAttackAbility : MonoBehaviour
    {
        [Header("Runtime Config (Overridden by Archetype if present)")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float speed = 15f;
        [SerializeField] private LayerMask hitMask;

        [SerializeField] private float arcHeight = 0f;

        private Transform _target;

        public void Configure(GameObject prefab, Transform spawn, float dmg, float spd, LayerMask mask, float arc = 0f)
        {
            projectilePrefab = prefab;
            spawnPoint = spawn;
            damage = dmg;
            speed = spd;
            hitMask = mask;
            arcHeight = arc;
        }

        public void SetTarget(Transform t)
        {
            _target = t;
        }

        // Called by Animator Event: "AnimEvent_ThrowProjectile"
        public void AnimEvent_ThrowProjectile()
        {
            if (projectilePrefab == null || spawnPoint == null)
            {
                Debug.LogWarning($"[ProjectileAttackAbility] Missing prefab or spawn point on {name}", this);
                if (spawnPoint == null) spawnPoint = transform; // weak fallback
                if (projectilePrefab == null) return;
            }

            // Use PoolManager if available
            GameObject projObj;
            if (PoolManager.Instance != null)
            {
                 projObj = PoolManager.Instance.Spawn(projectilePrefab, spawnPoint.position, spawnPoint.rotation);
            }
            else
            {
                 projObj = Instantiate(projectilePrefab, spawnPoint.position, spawnPoint.rotation);
            }

            // Calculate direction with arc if needed
            Vector3 direction = transform.forward;
            if (_target != null)
            {
                Vector3 toTarget = _target.position - spawnPoint.position;
                if (arcHeight > 0f)
                {
                     // Aim slightly up for arcs
                     toTarget.y += arcHeight;
                }
                direction = toTarget.normalized;
            }

            // Setup projectile logic
            var proj = projObj.GetComponent<IProjectile>();
            if (proj != null)
            {
                proj.Initialize(damage, speed, direction, hitMask, arcHeight);
            }
            else
            {
                // Legacy fallback for old scripts that use specific components
                // We'll try to find a rigidbody and just shoot it forward
                var rb = projObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Use the direction we already calculated above
                    rb.linearVelocity = direction * speed;
                }
            }
        }
    }
    
    public interface IProjectile
    {
        void Initialize(float damage, float speed, Vector3 direction, LayerMask mask, float arcHeight = 0f);
    }
}
