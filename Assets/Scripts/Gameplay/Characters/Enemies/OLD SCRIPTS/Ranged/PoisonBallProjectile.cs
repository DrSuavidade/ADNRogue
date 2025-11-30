using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Ranged
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class PoisonBallProjectile : MonoBehaviour
    {
        [Header("Vida")]
        public float lifeSeconds = 6f;

        [Header("Damage instantâneo (opcional)")]
        public float damageOnHit = 0f;

        [Header("Poison (igual ao Frog Toxicity)")]
        public float poisonDps = 1.0f;     // DPS aplicado enquanto envenenado
        public float poisonDuration = 4f;  // renova em cada hit (sem stacks)

        [Header("Layers que pode acertar")]
        public LayerMask hittable = ~0;

        [Header("Movimento")]
        public bool alignToVelocity = true;   // olha para a direção do movimento
        public bool horizontalOnly = true;    // sem gravidade, reto no plano

        [Header("VFX Poison Flash (opcional)")]
        public Color poisonFlashColor = new Color(0f, 0.85f, 0f, 1f);
        public float poisonFlashDuration = 0.05f;

        Rigidbody rb;
        Collider col;

        bool launched = false;
        float alive = 0f;

        Transform owner;
        float ownerRangeSqr = 900f; // default 30^2

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();

            col.isTrigger = true;
            ResetProjectile();
        }

        void OnEnable()
        {
            // IMPORTANTE para pooling
            ResetProjectile();
        }

        void ResetProjectile()
        {
            launched = false;
            alive = 0f;

            if (rb)
            {
                // FIX: não mexer em velocity quando já é kinematic
                if (!rb.isKinematic)
                {
                    rb.linearVelocity  = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                rb.isKinematic = true;
                rb.useGravity  = false;
            }

            if (col) col.enabled = false;
        }

        /// <summary>
        /// Lança o projétil. Passa o owner para controlar distância máxima.
        /// </summary>
        public void Launch(
            Vector3 velocity,
            Collider[] ignoreOwner,
            Transform ownerTf,
            float ownerRange)
        {
            launched = true;
            owner = ownerTf;
            ownerRangeSqr = ownerRange * ownerRange;

            rb.isKinematic = false;
            col.enabled = true;

            Vector3 v = velocity;

            if (horizontalOnly)
            {
                v.y = 0f;
                rb.useGravity = false;
            }
            else rb.useGravity = true;

            rb.linearVelocity = v;

            // Ignorar colisões com o inimigo que atirou
            if (ignoreOwner != null)
            {
                for (int i = 0; i < ignoreOwner.Length; i++)
                {
                    var c = ignoreOwner[i];
                    if (c != null)
                        Physics.IgnoreCollision(col, c, true);
                }
            }
        }

        void Update()
        {
            if (!launched) return;

            alive += Time.deltaTime;
            if (alive >= lifeSeconds)
            {
                Destroy(gameObject); // troca por pool.Release(this) se usares pooling
                return;
            }

            // Alinhar rotação à direção
            if (alignToVelocity && rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 dir = rb.linearVelocity;
                if (horizontalOnly) dir.y = 0f;

                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            // Se se afastar demasiado do dono → destruir
            if (owner)
            {
                Vector3 offset = transform.position - owner.position;
                if (offset.sqrMagnitude > ownerRangeSqr)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!launched) return;

            Transform root = other.attachedRigidbody
                ? other.attachedRigidbody.transform
                : other.transform;

            // Layer não permitida
            if ((hittable.value & (1 << root.gameObject.layer)) == 0)
            {
                Destroy(gameObject);
                return;
            }

            // Acertou no player
            var ph = root.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                // dano instantâneo opcional
                if (damageOnHit > 0f)
                    ph.ApplyDamage(damageOnHit);

                // aplicar / renovar veneno (sem stacks)
                var p = ph.GetComponent<PoisonStatusPlayer>();
                if (!p) p = ph.gameObject.AddComponent<PoisonStatusPlayer>();
                p.Apply(this);

                Destroy(gameObject);
                return;
            }

            // Acertou noutro objeto hittable
            Destroy(gameObject);
        }

        // =================== Poison state no PLAYER (no stacks, refresh on hit) ===================
        public class PoisonStatusPlayer : MonoBehaviour
        {
            PoisonBallProjectile def;
            float expireAt;
            bool ticking;

            public void Apply(PoisonBallProjectile d)
            {
                def = d;
                expireAt = Time.time + def.poisonDuration;

                if (!ticking) StartCoroutine(Tick());
            }

            IEnumerator Tick()
            {
                ticking = true;
                const float tickInterval = 0.5f;

                var ph = GetComponent<PlayerHealth>();

                while (Time.time < expireAt)
                {
                    if (ph != null)
                    {
                        // damage tick (DPS escalado pelo intervalo)
                        ph.ApplyDamage(def.poisonDps * tickInterval);

                        // flash verde opcional
                        var flash = ph.GetComponent<PoisonFlash>();
                        if (!flash) flash = ph.gameObject.AddComponent<PoisonFlash>();
                        flash.Trigger(def.poisonFlashDuration, def.poisonFlashColor);
                    }

                    yield return new WaitForSeconds(tickInterval);
                }

                ticking = false;
                Destroy(this);
            }
        }

        // =================== Flash verde simples, igual ao exemplo ===================
        class PoisonFlash : MonoBehaviour
        {
            static readonly int _ColorID     = Shader.PropertyToID("_Color");
            static readonly int _BaseColorID = Shader.PropertyToID("_BaseColor");

            Coroutine co;

            public void Trigger(float duration, Color flashColor)
            {
                if (co != null) StopCoroutine(co);
                co = StartCoroutine(Flash(duration, flashColor));
            }

            IEnumerator Flash(float duration, Color flashColor)
            {
                var rends = GetComponentsInChildren<Renderer>(true);
                if (rends == null || rends.Length == 0) yield break;

                var originals = new System.Collections.Generic.List<(Material mat, Color col, int prop)>();
                foreach (var r in rends)
                {
                    var mats = r.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i]; if (!m) continue;
                        int prop = m.HasProperty(_BaseColorID) ? _BaseColorID :
                                   (m.HasProperty(_ColorID) ? _ColorID : -1);
                        if (prop < 0) continue;

                        originals.Add((m, m.GetColor(prop), prop));
                        m.SetColor(prop, flashColor);
                    }
                }

                yield return new WaitForSeconds(Mathf.Max(0.01f, duration));

                foreach (var tuple in originals)
                    if (tuple.mat) tuple.mat.SetColor(tuple.prop, tuple.col);

                co = null;
            }

            void OnDisable() { if (co != null) { StopCoroutine(co); co = null; } }
            void OnDestroy() { if (co != null) { StopCoroutine(co); co = null; } }
        }
    }
}
