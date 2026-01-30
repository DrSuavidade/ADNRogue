using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies.Config; // <- para EnemyConfigurator

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class RomanDiscThrower : RomanEnemyAbilityBase
    {
        [Header("Tiro")]
        public float throwSpeed = 22f;
        public float arcHeight = 0.8f;
        public float damage = 12f;

        [Tooltip("Layers que o disco pode atingir.")]
        public LayerMask hitMask = ~0;

        EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
        }

        // Chamado pelo Animation Event na animação (ex: AnimEvent_ThrowDisc)
        public void AnimEvent_ThrowDisc()
        {
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
            
            if (_config == null || _config.Archetype == null) return;
            var settings = _config.Archetype.projectile;

            if (!settings.enabled || settings.projectilePrefab == null || !target)
                return;

            Transform origin = transform.Find("ProjectileSpawnPoint");
            if (origin == null) origin = transform;
            
            GameObject prefab = settings.projectilePrefab;

            var obj = Instantiate(prefab, origin.position, origin.rotation);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 to = target.position - origin.position;
                to.y += settings.arcHeight; // Use arcHeight from config
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

    // Igual ao teu, só deixei completo aqui
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
            // filtrar por layer
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            var hp = other.GetComponent<PlayerHealth>();
            if (hp != null)
                hp.ApplyDamage(damage);

            Destroy(gameObject);
        }
    }
}
