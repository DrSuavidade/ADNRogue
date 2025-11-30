using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Characters.Enemies; // para encontrar o dono

namespace Geneforge.Gameplay.Characters.Enemies.Ranged
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class SpearProjectile : MonoBehaviour
    {
        [Header("Flight")]
        public float lifeSeconds = 8f;
        public bool alignToVelocity = true;
        [Range(0f, 30f)] public float launchPitchDegrees = 10f; // (IGNORADO no voo horizontal)

        [Header("Damage")]
        public int damage = 20;
        public LayerMask hittable = ~0;

        [Header("Auto-Despawn (quando falha)")]
        public bool clampByOwnerRange = true;
        public float ownerRangeFallback = 30f;     // usado se não lermos do Ranged
        public bool killWhenBehindOwner = true;    // destrói quando passa para trás do atirador

        Rigidbody rb;
        Collider col;
        bool launched, hasHit;
        float alive;

        Transform owner;           // inimigo que lançou
        float ownerRangeSqr;       // attackRange^2 (ou fallback)
        Vector3 spawnPos;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();

            rb.isKinematic = true;      // começa presa na mão
            col.enabled = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Encontrar o dono (está como pai enquanto a lança está na mão)
            var ranged = GetComponentInParent<Ranged>();
            owner = ranged ? ranged.transform : GetComponentInParent<Enemy>()?.transform;

            // Ler o alcance do Ranged se existir
            float range = ranged ? Mathf.Max(0.1f, ranged.attackRange) : ownerRangeFallback;
            ownerRangeSqr = range * range;

            spawnPos = transform.position;
        }

        public void Launch(Vector3 velocity, Collider[] ignoreOwner = null)
        {
            launched = true;

            rb.isKinematic = false;
            col.enabled = true;
            col.isTrigger = true;    // funciona com CharacterController

            // ========= VOO HORIZONTAL =========
            // Ignora o pitch balístico e a gravidade; força direção plana (XZ).
            Vector3 v = velocity;
            v.y = 0f; // remove componente vertical
            if (v.sqrMagnitude < 1e-6f)
            {
                // fallback: usa o forward atual no plano
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                v = fwd.sqrMagnitude > 1e-6f ? fwd.normalized * Mathf.Max(1f, velocity.magnitude)
                                             : Vector3.forward * Mathf.Max(1f, velocity.magnitude);
            }

            rb.useGravity = false;   // sem gravidade → trajeto reto/horizontal
            rb.linearVelocity = v;

            // ignorar colisões com o dono
            if (ignoreOwner != null)
                foreach (var c in ignoreOwner)
                    if (c) Physics.IgnoreCollision(col, c, true);

            spawnPos = transform.position; // regista origem real do voo
        }

        void Update()
        {
            if (!launched) return;

            alive += Time.deltaTime;
            if (alive >= lifeSeconds) { DestroyProjectile(); return; }

            // Alinha a rotação com a velocidade, mantendo-a horizontal
            if (alignToVelocity && rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 dir = rb.linearVelocity;
                dir.y = 0f; // força orientação na horizontal
                if (dir.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            // —— Auto-despawn por alcance / passou para trás —— 
            if (owner && clampByOwnerRange)
            {
                Vector3 toProj = transform.position - owner.position;

                // fora do raio do dono (≈ attackRange)
                if (toProj.sqrMagnitude > ownerRangeSqr)
                {
                    DestroyProjectile();
                    return;
                }

                // passou para trás do dono (cruzou plano traseiro)
                if (killWhenBehindOwner && Vector3.Dot(owner.forward, toProj) < 0f)
                {
                    DestroyProjectile();
                    return;
                }
            }
        }

        // Trigger: acerto no Player
        void OnTriggerEnter(Collider other)
        {
            if (!launched || hasHit) return;

            Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;

            // não-hittable -> falha: destruir
            if ((hittable.value & (1 << root.gameObject.layer)) == 0)
            {
                DestroyProjectile();
                return;
            }

            var ph = root.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                hasHit = true;
                ph.ApplyDamage(damage);
                DestroyProjectile();        // não cola no Player
                return;
            }

            // Outros "hittable" sem PlayerHealth: falha -> destruir
            DestroyProjectile();
        }

        // (opcional) se em algum caso não for trigger
        void OnCollisionEnter(Collision other)
        {
            if (!launched || hasHit) return;

            Transform root = other.collider.attachedRigidbody ? other.collider.attachedRigidbody.transform : other.transform;

            if ((hittable.value & (1 << root.gameObject.layer)) != 0)
            {
                var ph = root.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    hasHit = true;
                    ph.ApplyDamage(damage);
                    DestroyProjectile();
                    return;
                }
            }

            DestroyProjectile();
        }

        void DestroyProjectile()
        {
            col.enabled = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
            Destroy(gameObject);
        }
    }
}
