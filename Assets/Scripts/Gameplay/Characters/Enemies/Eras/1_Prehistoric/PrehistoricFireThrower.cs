using UnityEngine;

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

        // Animation event
        public void AnimEvent_ThrowTorch()
        {
            if (!torchProjectilePrefab || !throwOrigin || !target) return;

            var projObj = Object.Instantiate(
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

            proj.Init(impactDamage);
        }
    }

    /// <summary>
    /// Simple fire projectile that damages the player on hit.
    /// You can expand this (burning ground, DOT, etc.) later.
    /// </summary>
    public class PrehistoricFireProjectile : MonoBehaviour
    {
        float damage;

        public void Init(float dmg)
        {
            damage = dmg;
            Destroy(gameObject, 6f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                var hp = other.GetComponent<Geneforge.Gameplay.Characters.Player.PlayerHealth>();
                if (hp) hp.ApplyDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
