using UnityEngine;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanDiscThrower : RomanEnemyAbilityBase
    {
        [Header("Disc")]
        public GameObject discPrefab;
        public Transform throwOrigin;
        public float throwSpeed = 22f;
        public float arcHeight = 0.8f;
        public float damage = 12f;

        [Tooltip("Layers que o disco pode atingir.")]
        public LayerMask hitMask = ~0;

        public void AnimEvent_ThrowDisc()
        {
            if (!discPrefab || !throwOrigin || !target) return;

            var obj = Instantiate(discPrefab, throwOrigin.position, throwOrigin.rotation);
            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 to = target.position - throwOrigin.position;
                to.y += arcHeight;
                Vector3 vel = to.normalized * throwSpeed;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = vel;
#else
                rb.velocity = vel;
#endif
            }

            var proj = obj.GetComponent<RomanDiscProjectile>();
            if (!proj) proj = obj.AddComponent<RomanDiscProjectile>();
            proj.Init(damage, hitMask);
        }
    }

    public class RomanDiscProjectile : MonoBehaviour
    {
        float damage;
        LayerMask hitMask;

        public void Init(float dmg, LayerMask mask)
        {
            damage = dmg;
            hitMask = mask;
            Destroy(gameObject, 8f);
        }

        void OnTriggerEnter(Collider other)
        {
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            var hp = other.GetComponent<PlayerHealth>();
            if (hp != null)
                hp.ApplyDamage(damage);

            Destroy(gameObject);
        }
    }
}
