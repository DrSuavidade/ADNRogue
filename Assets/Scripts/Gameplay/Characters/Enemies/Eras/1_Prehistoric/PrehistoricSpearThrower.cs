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
            proj.Init(damage);
        }
    }

    public class PrehistoricSpearProjectile : MonoBehaviour
    {
        float damage;

        public void Init(float dmg)
        {
            damage = dmg;
            Destroy(gameObject, 8f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                var hp = other.GetComponent<Player.PlayerHealth>();
                if (hp) hp.ApplyDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
