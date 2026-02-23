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

        // Chamado pelo Animation Event na animação de ataque 1
        public void AnimEvent_ThrowDisc()
        {
            ThrowSingleDisc(0f);
        }

        // NOVO: Chamado pelo Animation Event no Attack2
        // Dispara 3 discos em leque para ser mais difícil mas ainda desviável
        public void AnimEvent_ThrowDiscVolley()
        {
            ThrowSingleDisc(0f);    // Centro
            ThrowSingleDisc(-18f);  // Esquerda
            ThrowSingleDisc(18f);   // Direita
        }

        private void ThrowSingleDisc(float angleOffset)
        {
            if (_config == null) _config = GetComponent<EnemyConfigurator>();
            if (_config == null || _config.Archetype == null) return;
            
            var settings = _config.Archetype.projectile;
            if (!settings.enabled || settings.projectilePrefab == null || !target) return;

            Transform origin = transform.Find("ProjectileSpawnPoint");
            if (origin == null) origin = transform;
            
            Vector3 to = target.position - origin.position;
            to.y += settings.arcHeight;
            Vector3 dir = Quaternion.Euler(0, angleOffset, 0) * to.normalized;

            // CORREÇÃO: Pegamos na direção mas "limpamos" a inclinação vertical (Y) 
            // para que o disco se mantenha horizontal (estilo frisbee) e não aponte para cima.
            Vector3 flatDir = dir;
            flatDir.y = 0;
            if (flatDir.sqrMagnitude < 0.0001f) flatDir = transform.forward;
            
            Quaternion spawnRot = Quaternion.LookRotation(flatDir);

            GameObject obj = Instantiate(settings.projectilePrefab, origin.position, spawnRot);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 vel = dir * throwSpeed;
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

    // Projétil com efeito de rotação (Spin)
    public class RomanDiscProjectile : MonoBehaviour
    {
        float damage;
        LayerMask hitMask;
        public float rotationSpeed = 1200f; // Velocidade do spin

        public void Init(float dmg, LayerMask mask)
        {
            damage = dmg;
            hitMask = mask;
            Destroy(gameObject, 8f);
        }

        void Update()
        {
            // Profissional: O disco deve manter-se sempre perfeitamente horizontal (paralelo ao chão)
            // Independentemente da direção do projétil, limpamos rotações em X e Z.
            Vector3 currentRotation = transform.eulerAngles;
            float newYaw = currentRotation.y + rotationSpeed * Time.deltaTime;
            
            // Forçamos X=0 e Z=0 para ser um "frisbee" profissional
            transform.rotation = Quaternion.Euler(0, newYaw, 0);
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
