using UnityEngine;
///Develop
namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    public class PrehistoricFireThrower : PrehistoricEnemyAbilityBase
    {
        [Header("Projectile")]
        public GameObject torchProjectilePrefab;
        public Transform throwOrigin;
        public float throwSpeed = 18f;
        public float arcHeight = 1.5f;

        [Header("Damage")]
        public float impactDamage = 6f;
        [Tooltip("What layers this torch can damage (usually Player).")]
        public LayerMask hitMask;

        // Animation event
        public void AnimEvent_ThrowTorch()
        {
            if (!torchProjectilePrefab || !throwOrigin || !target) return;

            var projObj = Instantiate(
                torchProjectilePrefab,
                throwOrigin.position,
                Quaternion.identity
            );

            var rb = projObj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 toTarget = target.position - throwOrigin.position;
                toTarget.y += arcHeight;
                Vector3 vel = toTarget.normalized * throwSpeed;
                rb.linearVelocity = vel;
            }

            var proj = projObj.GetComponent<PrehistoricFireProjectile>();
            if (!proj)
                proj = projObj.AddComponent<PrehistoricFireProjectile>();

            proj.Init(impactDamage, hitMask);
        }
    }

    /// <summary>
    /// Simple fire projectile that damages the player on hit.
    /// You can expand this (burning ground, DOT, etc.) later.
    /// </summary>
    public class PrehistoricFireProjectile : MonoBehaviour
    {
        float damage;
        LayerMask hitMask;

        public void Init(float dmg, LayerMask mask)
        {
            damage = dmg;
            hitMask = mask;
            Destroy(gameObject, 6f);
        }

        void OnTriggerEnter(Collider other)
        {
            // First, check if this collider is on a hittable layer
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            var hp = other.GetComponent<Player.PlayerHealth>();
            if (hp != null)
            {
                hp.ApplyDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}
