using UnityEngine;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanArcher : RomanEnemyAbilityBase
    {
        [Header("Arrow")]
        public GameObject arrowPrefab;
        public Transform shootOrigin;
        public float arrowSpeed = 26f;
        public float arcHeight = 0.5f;
        public float damage = 8f;

        [Tooltip("Que layers a seta pode atingir (normalmente Player).")]
        public LayerMask hitMask = ~0;

        // Chamado por evento de animação
        public void AnimEvent_ShootArrow()
        {
            if (!arrowPrefab || !shootOrigin || !target) return;

            var obj = Instantiate(arrowPrefab, shootOrigin.position, shootOrigin.rotation);
            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 to = target.position - shootOrigin.position;
                to.y += arcHeight;
                Vector3 vel = to.normalized * arrowSpeed;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = vel;
#else
                rb.velocity = vel;
#endif
            }

            var proj = obj.GetComponent<RomanArrowProjectile>();
            if (!proj) proj = obj.AddComponent<RomanArrowProjectile>();
            proj.Init(damage, hitMask);
        }
    }

    /// <summary>
    /// Projectil simples da seta – voa em linha e dá dano ao player.
    /// </summary>
    public class RomanArrowProjectile : MonoBehaviour
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
            // Filtrar layers
            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            var hp = other.GetComponent<PlayerHealth>();
            if (hp != null)
                hp.ApplyDamage(damage);

            Destroy(gameObject);
        }
    }
}
