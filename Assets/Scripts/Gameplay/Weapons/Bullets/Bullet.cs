using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Geneforge.Gameplay.Characters.Enemies;


namespace Geneforge.Gameplay.Weapons.Bullets 
{
    public class Bullet : MonoBehaviour
    {
        public float lifeTime = 3f;
        [HideInInspector] public float damage = 1f;
        [HideInInspector] public float knockbackForce = 0f;  // e.g. 1 → 0.1f, 0.5 → 0.05f
        [HideInInspector] public bool isCrit = false;
        public GameObject impactEffectPrefab;

        // Chain lightning settings (on-hit)
        [Header("Chain Lightning (On Hit)")]
        [Tooltip("Enable chain lightning when this projectile hits any enemy")]
        public bool enableChainLightning = true;

        [Tooltip("Radius to search the next enemy per hop")]
        public float chainLightningRadius = 6f;

        [Tooltip("Damage dealt on each chained hop (separado do dano inicial da bala)")]
        public float chainLightningDamage = 10f;

        [Tooltip("VFX usado para cada ligação ou impacto. Se tiver LineRenderer, é usado como 'raio' entre alvos.")]
        public GameObject chainLightningEffectPrefab;

        // 🔹 NOVO: parâmetros para comportamento tipo Electro Spirit (saltos sequenciais)
        [Header("Electro Spirit Style")]
        [Tooltip("Número máximo de saltos após o primeiro inimigo atingido")]
        public int chainMaxJumps = 12;

        [Tooltip("Atraso entre cada salto (efeito de 'travadinha' entre alvos)")]
        public float chainDelayBetweenJumps = 0.32f;

        // Internos
        private bool _chainRunning = false;

        void Awake()
        {
            // Destroi só a bala ao fim de X; os efeitos instanciados são independentes
            Destroy(transform.root.gameObject, lifeTime);
        }

        void OnCollisionEnter(Collision collision)
        {
            // 1) Impact VFX (como tinhas)
            if (impactEffectPrefab != null && collision.contacts.Length > 0)
            {
                var cp = collision.contacts[0];
                var fx = Instantiate(impactEffectPrefab, cp.point, Quaternion.LookRotation(cp.normal));
                var ps = fx.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    Destroy(fx, main.duration + main.startLifetime.constantMax);
                }
                else Destroy(fx, 2f);
            }

            // 2) Dano + knockback no primeiro alvo atingido
            var enemy = collision.collider.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, isCrit);

                if (knockbackForce > 0f)
                {
                    Vector3 dir = transform.forward; dir.y = 0f;
                    enemy.ApplyKnockback(dir, knockbackForce);
                }

                // 3) Em vez de AOE, iniciamos a COROUTINE de saltos sequenciais
                if (enableChainLightning && !_chainRunning)
                {
                    StartCoroutine(ChainLightningSequence(enemy.transform));
                    // Desativamos o visual/colisão da bala para não interferir, e destruímos no fim da cadeia
                    HideAndDisableBullet();
                    return;
                }
            }

            // Se não iniciou cadeia, destruímos já a bala
            Destroy(transform.root.gameObject);
        }

        // 🔹 Eletro Spirit: salta de inimigo em inimigo dentro do raio, sempre para o mais próximo, com delay entre saltos
        private IEnumerator ChainLightningSequence(Transform firstTarget)
        {
            _chainRunning = true;

            // Conjunto de alvos já atingidos (para não repetir)
            var visited = new HashSet<Transform>();
            Transform current = firstTarget;

            visited.Add(current);

            // VFX no primeiro impacto (opcional)
            SpawnImpactVFXAt(current.position);

            int jumps = 0;

            while (current != null && jumps < chainMaxJumps)
            {
                // Procurar próximo inimigo mais próximo dentro do raio
                Collider[] hits = Physics.OverlapSphere(current.position, chainLightningRadius);
                Transform next = null;
                float bestDist = Mathf.Infinity;

                for (int i = 0; i < hits.Length; i++)
                {
                    var col = hits[i];
                    var otherEnemy = col.GetComponent<Enemy>();
                    if (otherEnemy == null) continue;

                    Transform t = otherEnemy.transform;
                    if (visited.Contains(t)) continue;

                    float d = Vector3.Distance(current.position, t.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        next = t;
                    }
                }

                if (next == null) break; // acabou a cadeia (nenhum válido no raio)

                // Dano no próximo
                var nextEnemy = next.GetComponent<Enemy>();
                if (nextEnemy != null)
                    nextEnemy.TakeDamage(chainLightningDamage);

                // VFX de ligação (raio) entre current → next
                SpawnLinkVFX(current.position, next.position);

                visited.Add(next);
                current = next;
                jumps++;

                // Pequena pausa entre saltos para “vender” o efeito
                if (chainDelayBetweenJumps > 0f)
                    yield return new WaitForSeconds(chainDelayBetweenJumps);
                else
                    yield return null;
            }

            // Fim: destruir a bala (agora já escondida/desativada)
            Destroy(transform.root.gameObject);
        }

        // --- Helpers de VFX ---
        void SpawnImpactVFXAt(Vector3 pos)
        {
            if (chainLightningEffectPrefab == null) return;

            // Se o prefab for um ParticleSystem de impacto, instanciamos no ponto
            var fx = Instantiate(chainLightningEffectPrefab, pos, Quaternion.identity);
            var ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                Destroy(fx, main.duration + main.startLifetime.constantMax);
            }
            else
            {
                // Se não tiver PS, auto-destroi rápido
                Destroy(fx, 0.35f);
            }
        }

        void SpawnLinkVFX(Vector3 from, Vector3 to)
        {
            if (chainLightningEffectPrefab == null) return;

            var fx = Instantiate(chainLightningEffectPrefab);
            // Se o prefab tiver LineRenderer, usamos como “raio”
            var lr = fx.GetComponent<LineRenderer>();
            if (lr != null)
            {
                if (lr.positionCount != 2) lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
                Destroy(fx, 0.3f);
                return;
            }

            // Caso contrário, colocamos no meio só para ter um efeito (fallback)
            fx.transform.position = (from + to) * 0.5f;
            var ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                Destroy(fx, main.duration + main.startLifetime.constantMax);
            }
            else Destroy(fx, 0.35f);
        }

        // Esconde a bala e desliga colisão para deixar a coroutine correr até ao fim
        void HideAndDisableBullet()
        {
            var col = GetComponent<Collider>();
            if (col) col.enabled = false;

            var rend = GetComponentInChildren<Renderer>();
            if (rend) rend.enabled = false;

            // se a bala tiver RigidBody, pára-a
            var rb = GetComponent<Rigidbody>();
            if (rb) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
        }
    }
}
