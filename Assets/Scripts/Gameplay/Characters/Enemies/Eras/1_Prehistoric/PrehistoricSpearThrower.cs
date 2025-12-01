using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    public class PrehistoricSpearThrower : PrehistoricEnemyAbilityBase
    {
        [Header("Spear")]
        public GameObject spearPrefab;
        public Transform throwOrigin;
        public float throwSpeed = 22f;
        public float arcHeight = 1.0f;
        public float damage = 10f;

        [Tooltip("What layers this spear can damage (usually Player).")]
        public LayerMask hitMask;


        public void AnimEvent_ThrowSpear()
        {
            if (!spearPrefab || !throwOrigin || !target) return;

            var obj = Object.Instantiate(spearPrefab, throwOrigin.position, throwOrigin.rotation);
            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 to = target.position - throwOrigin.position;
                to.y += arcHeight;
                Vector3 vel = to.normalized * throwSpeed;
                rb.linearVelocity = vel;
            }

            var proj = obj.GetComponent<PrehistoricSpearProjectile>();
            if (!proj) proj = obj.AddComponent<PrehistoricSpearProjectile>();
            proj.Init(damage, hitMask);
        }
    }

    public class PrehistoricSpearProjectile : MonoBehaviour
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
            // Layer filter first
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
