using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
///Develop
namespace Geneforge.Gameplay.Characters.Enemies.Eras.Roman
{
    [RequireComponent(typeof(EnemyCore))]
    public class RomanArcher : RomanEnemyAbilityBase
    {
        [Header("Projectile Settings")]
        public GameObject arrowPrefab;
        public Transform shootOrigin;
        public float arrowSpeed = 26f;
        public float arcHeight = 1.5f; // Aumentado para um arco mais visível
        public float damage = 8f;
        public bool useGravity = true;

        [Header("Visual Correction")]
        [Tooltip("Se a seta voa de lado, mude o Y para 90. Se voa de costas, 180.")]
        public float yawOffset = 90f;

        [Header("Aiming")]
        [Tooltip("Velocidade com que o arqueiro gira para encarar o player.")]
        public float turnSpeed = 10f;
        [Tooltip("Se ativado, o arqueiro vira instantaneamente para o player no momento do tiro.")]
        public bool snapToTargetOnFire = true;

        [Tooltip("Que layers a seta pode atingir.")]
        public LayerMask hitMask = ~0;

        protected virtual void Update()
        {
            if (target != null)
            {
                // Rotação suave em direção ao player (apenas no eixo Y)
                Vector3 lookPos = target.position - transform.position;
                lookPos.y = 0; // Mantém o inimigo em pé
                
                if (lookPos.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookPos);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
            }
        }

        // Chamado por evento de animação
        public void AnimEvent_ShootArrow()
        {
            if (!arrowPrefab || !shootOrigin || !target) return;

            // SNAP: Ajuste instantâneo para não sair de lado se o player se moveu rápido
            if (snapToTargetOnFire)
            {
                Vector3 finalLook = target.position - transform.position;
                finalLook.y = 0;
                if (finalLook.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(finalLook);
                }
            }

            // Alvo na altura do peito/centro
            Vector3 targetPos = target.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetPos - shootOrigin.position;
            
            // Direção horizontal para orientação
            Vector3 flatDir = toTarget;
            flatDir.y = 0;
            if (flatDir.sqrMagnitude < 0.001f) flatDir = transform.forward;
            
            // Calculamos o ângulo Y (Yaw) para o player e somamos o offset do modelo
            float angleY = Quaternion.LookRotation(flatDir).eulerAngles.y + yawOffset;
            Quaternion spawnRot = Quaternion.Euler(0, angleY, 0);

            var obj = Instantiate(arrowPrefab, shootOrigin.position, spawnRot);
            
            var rb = obj.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.useGravity = useGravity;
                // Travamos rotações para evitar capotamento físico
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                Vector3 velocity = toTarget.normalized * arrowSpeed;
                if (useGravity) velocity.y += arcHeight;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = velocity;
#else
                rb.velocity = velocity;
#endif
            }

            var proj = obj.GetComponent<RomanArrowProjectile>();
            if (!proj) proj = obj.AddComponent<RomanArrowProjectile>();
            proj.Init(damage, hitMask, spawnRot.eulerAngles.y);
        }
    }

    public class RomanArrowProjectile : MonoBehaviour
    {
        private float damage;
        private LayerMask hitMask;
        private float _fixedYaw;
        private Rigidbody _rb;
        private bool _isInitialized;

        public void Init(float dmg, LayerMask mask, float yaw)
        {
            damage = dmg;
            hitMask = mask;
            _fixedYaw = yaw;
            _rb = GetComponent<Rigidbody>();
            _isInitialized = true;
            Destroy(gameObject, 6f);
        }

        void FixedUpdate()
        {
            if (!_isInitialized || _rb == null) return;

            // Mantém a seta sempre horizontal e apontando para a mesma direção Y (estilo Disco)
            // Isso remove qualquer tremor ou rotação estranha da física
            _rb.MoveRotation(Quaternion.Euler(0, _fixedYaw, 0));
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_isInitialized) return;

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
