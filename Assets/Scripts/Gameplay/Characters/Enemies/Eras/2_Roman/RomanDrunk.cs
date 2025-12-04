using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies.Config;   // <- EnemyConfigurator

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    [RequireComponent(typeof(EnemyConfigurator))]
    public class RomanDrunk : RomanEnemyAbilityBase
    {
        [Header("Wine Bottle Throw")]
        public float throwSpeed = 20f;
        public float arcHeight = 1.5f;
        public float impactDamage = 6f;
        public float splashRadius = 1.8f;   // mini AoE de estilhaços

        [Tooltip("Layers afectadas pela garrafa.")]
        public LayerMask hitMask = ~0;

        private EnemyConfigurator _config;

        protected override void Awake()
        {
            base.Awake();
            _config = GetComponent<EnemyConfigurator>();
        }

        // Chamado pelo Animation Event na animação do bêbado (ex: AnimEvent_ThrowBottle)
        public void AnimEvent_ThrowBottle()
        {
            if (_config == null)
                _config = GetComponent<EnemyConfigurator>();

            // Usa as ThrowSettings do EnemyConfigurator (Ranged Visual/Spawn Setup)
            var settings = _config.ThrowSettings;
            if (settings == null || !settings.spearPrefab || !settings.throwOrigin || !target)
                return;

            Transform origin = settings.throwOrigin;
            GameObject prefab = settings.spearPrefab;

            var obj = Instantiate(prefab, origin.position, origin.rotation);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 to = target.position - origin.position;
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
            var cols = Physics.OverlapSphere(
                transform.position,
                radius,
                hitMask,
                QueryTriggerInteraction.Ignore
            );

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
