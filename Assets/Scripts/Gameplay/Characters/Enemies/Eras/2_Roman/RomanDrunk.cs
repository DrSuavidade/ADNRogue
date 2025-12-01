using UnityEngine;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanDrunk : RomanEnemyAbilityBase
    {
        [Header("Wine Bottle")]
        public GameObject bottlePrefab;
        public Transform throwOrigin;
        public float throwSpeed = 20f;
        public float arcHeight = 1.5f;
        public float impactDamage = 6f;
        public float splashRadius = 1.8f;   // mini AoE de estilhaços

        public LayerMask hitMask = ~0;

        public void AnimEvent_ThrowBottle()
        {
            if (!bottlePrefab || !throwOrigin || !target) return;

            var obj = Instantiate(bottlePrefab, throwOrigin.position, throwOrigin.rotation);
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

            var proj = obj.GetComponent<RomanWineBottleProjectile>();
            if (!proj) proj = obj.AddComponent<RomanWineBottleProjectile>();
            proj.Init(impactDamage, splashRadius, hitMask);
        }
    }

    public class RomanWineBottleProjectile : MonoBehaviour
    {
        float damage;
        float radius;
        LayerMask hitMask;

        public void Init(float dmg, float r, LayerMask mask)
        {
            damage = dmg;
            radius = r;
            hitMask = mask;
            Destroy(gameObject, 6f);
        }

        void OnTriggerEnter(Collider other)
        {
            // Quando a garrafa bate em algo válido → explode
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            Explode();
        }

        void Explode()
        {
            var cols = Physics.OverlapSphere(transform.position, radius, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var hp = cols[i].GetComponentInParent<PlayerHealth>();
                if (hp != null)
                    hp.ApplyDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}
