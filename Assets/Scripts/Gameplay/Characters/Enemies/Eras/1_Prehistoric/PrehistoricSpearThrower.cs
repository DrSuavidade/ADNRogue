using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies.Config; // <--- IMPORTANTE

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class PrehistoricSpearThrower : PrehistoricEnemyAbilityBase
    {
        [Header("Spear Flight")]
        public float throwSpeed = 22f;
        public float arcHeight = 1.0f;
        public float damage = 10f;

        [Tooltip("What layers this spear can damage (usually Player).")]
        public LayerMask hitMask;

        EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();
        }

        // Chamado pelo Animation Event "AnimEvent_ThrowSpear"
        public void AnimEvent_ThrowSpear()
        {
            if (_config == null) _config = GetComponent<EnemyConfigurator>();

            var settings = _config.ThrowSettings;
            if (settings == null || !settings.spearPrefab || !settings.throwOrigin || !target)
                return;

            var origin = settings.throwOrigin;
            var prefab = settings.spearPrefab;

            var obj = Object.Instantiate(prefab, origin.position, origin.rotation);
            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.isKinematic = false;

                Vector3 to = target.position - origin.position;
                to.y += arcHeight;
                Vector3 vel = to.normalized * throwSpeed;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = vel;
#else
                rb.velocity = vel;
#endif
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
            // 1) filtrar pela Layer
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            // 2) procurar vida no PAI também (muito importante!)
            var hp = other.GetComponentInParent<Player.PlayerHealth>();
            if (hp != null)
            {
                hp.ApplyDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}
