using UnityEngine;
using System.Collections.Generic;
using Geneforge.Gameplay.Characters.Enemies;

namespace Geneforge.Gameplay.Characters.Enemies.Eras.Prehistoric
{
    [RequireComponent(typeof(EnemyCore))]
    public class PrehistoricMeditation : PrehistoricEnemyAbilityBase
    {
        [Header("Meditation Shield")]
        public float shieldRadius = 8f;
        public float shieldDuration = 5f;
        [Tooltip("0 = todos os aliados encontrados")]
        public int maxAlliesPerCast = 0;
        public LayerMask allyMask = ~0;

        [Header("Layer (Opcional)")]
        [Tooltip("Layer que os ataques do jogador ignoram (ex.: 'Shielded'). Deixa -1 para não trocar.")]
        public int shieldedLayer = -1;

        [Header("Visual do Círculo")]
        public Vector3 indicatorOffset = new Vector3(0f, 2.0f, 0f);
        public float circleRadius = 0.5f;
        public int circleSegments = 24;
        public float circleWidth = 0.05f;

        static readonly Collider[] overlapBuffer = new Collider[96];

        // Estrutura para guardar os dados de cada escudo ativo
        class ShieldData
        {
            public Transform root;
            public float endTime;
            public Rigidbody rb;
            public bool prevIsKinematic;
            public CharacterController cc;
            public bool prevCCEnabled;
            public List<Collider> colliders = new List<Collider>(32);
            public List<bool> prevEnabled = new List<bool>(32);
            public Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>(64);
            public string originalTag;
            public GameObject circleObj;
            public LineRenderer lr;
        }

        readonly Dictionary<Transform, ShieldData> activeShields = new Dictionary<Transform, ShieldData>();

        // --------------------------------------------------------------------
        // EVENTO DE ANIMAÇÃO: Chamado pelo Animation Event "AnimEvent_Meditate"
        // --------------------------------------------------------------------
        public void AnimEvent_Meditate()
        {
            // Debug.Log("AnimEvent_Meditate disparado em " + name);

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                shieldRadius,
                overlapBuffer,
                allyMask,
                QueryTriggerInteraction.Ignore
            );

            if (count <= 0)
                return;

            List<Transform> candidates = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (!col) continue;

                var root = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;

                // ignora o próprio Meditation
                var ally = root.GetComponentInParent<EnemyCore>();
                if (ally == null || ally == enemy) continue;
                if (ally.CurrentHealth <= 0f) continue;

                if (!candidates.Contains(ally.transform))
                    candidates.Add(ally.transform);
            }

            // Ordenar por distância
            candidates.Sort((a, b) =>
            {
                float da = (a.position - transform.position).sqrMagnitude;
                float db = (b.position - transform.position).sqrMagnitude;
                return da.CompareTo(db);
            });

            int applied = 0;
            foreach (var t in candidates)
            {
                if (ApplyShieldToTarget(t))
                {
                    applied++;
                    if (maxAlliesPerCast > 0 && applied >= maxAlliesPerCast)
                        break;
                }
            }
        }

        // --------------------------------------------------------------------
        // Função principal que aplica o escudo (cópia do Suporte)
        // --------------------------------------------------------------------
        bool ApplyShieldToTarget(Transform target)
        {
            if (!target) return false;

            ShieldData data;
            if (!activeShields.TryGetValue(target, out data))
            {
                data = new ShieldData { root = target };

                // 1) Desligar colliders (invulnerável a overlaps/hitboxes)
                data.colliders.Clear();
                data.prevEnabled.Clear();
                target.GetComponentsInChildren(true, data.colliders);
                foreach (var c in data.colliders)
                {
                    if (!c) { data.prevEnabled.Add(false); continue; }
                    data.prevEnabled.Add(c.enabled);
                    c.enabled = false;
                }

                // 2) Desativar CharacterController
                data.cc = target.GetComponentInParent<CharacterController>();
                if (data.cc != null)
                {
                    data.prevCCEnabled = data.cc.enabled;
                    data.cc.enabled = false;
                }

                // 3) Rigidbody kinematic (não é empurrado nem cai)
                data.rb = target.GetComponentInParent<Rigidbody>();
                if (data.rb != null)
                {
                    data.prevIsKinematic = data.rb.isKinematic;
                    data.rb.isKinematic = true;
                    data.rb.linearVelocity = Vector3.zero;
                    data.rb.angularVelocity = Vector3.zero;
                }

                // 4) Mudar layer (opcional)
                data.originalLayers.Clear();
                if (shieldedLayer >= 0)
                    RecordAndSetLayerRecursively(target, data.originalLayers, shieldedLayer);

                // 5) Tag → "Untagged" (para evitar ser alvejado)
                var go = target.gameObject;
                data.originalTag = go.tag;
                go.tag = "Untagged";

                // 6) Criar círculo visual acima do aliado
                data.circleObj = new GameObject("ShieldCircle");
                data.lr = data.circleObj.AddComponent<LineRenderer>();
                data.lr.useWorldSpace = true;
                data.lr.loop = true;
                data.lr.widthMultiplier = circleWidth;
                data.lr.material = new Material(Shader.Find("Sprites/Default"));
                data.lr.positionCount = circleSegments;

                activeShields.Add(target, data);
            }

            data.endTime = Time.time + shieldDuration;
            return true;
        }

        void Update()
        {
            UpdateActiveShields();
        }

        void UpdateActiveShields()
        {
            if (activeShields.Count == 0) return;

            var toRemove = new List<Transform>();

            foreach (var kv in activeShields)
            {
                var data = kv.Value;
                if (!data.root) { toRemove.Add(kv.Key); continue; }

                // Atualizar posição do círculo
                if (data.circleObj && data.lr)
                {
                    Vector3 center = data.root.position + indicatorOffset;
                    for (int i = 0; i < circleSegments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / circleSegments;
                        float x = Mathf.Cos(angle) * circleRadius;
                        float z = Mathf.Sin(angle) * circleRadius;
                        data.lr.SetPosition(i, center + new Vector3(x, 0f, z));
                    }
                }

                // Escudo expirou?
                if (Time.time >= data.endTime)
                {
                    // 1) Restaurar colliders
                    for (int i = 0; i < data.colliders.Count; i++)
                    {
                        var c = data.colliders[i];
                        if (!c) continue;
                        bool prev = (i < data.prevEnabled.Count) ? data.prevEnabled[i] : true;
                        c.enabled = prev;
                    }
                    data.colliders.Clear();
                    data.prevEnabled.Clear();

                    // 2) Restaurar CharacterController
                    if (data.cc != null) data.cc.enabled = data.prevCCEnabled;

                    // 3) Restaurar Rigidbody
                    if (data.rb != null) data.rb.isKinematic = data.prevIsKinematic;

                    // 4) Restaurar Layers
                    RestoreLayers(data.originalLayers);

                    // 5) Restaurar Tag
                    if (data.root) data.root.gameObject.tag = data.originalTag;

                    // 6) Destruir círculo visual
                    if (data.circleObj) Destroy(data.circleObj);

                    toRemove.Add(kv.Key);
                }
            }

            foreach (var t in toRemove) activeShields.Remove(t);
        }

        // --------------------------------------------------------------------
        // HELPERS
        // --------------------------------------------------------------------
        void RecordAndSetLayerRecursively(Transform root, Dictionary<Transform, int> store, int newLayer)
        {
            if (!root) return;
            if (!store.ContainsKey(root))
                store.Add(root, root.gameObject.layer);
            if (newLayer >= 0)
                root.gameObject.layer = newLayer;

            for (int i = 0; i < root.childCount; i++)
                RecordAndSetLayerRecursively(root.GetChild(i), store, newLayer);
        }

        void RestoreLayers(Dictionary<Transform, int> store)
        {
            if (store == null) return;
            foreach (var kv in store)
            {
                var t = kv.Key;
                if (t) t.gameObject.layer = kv.Value;
            }
            store.Clear();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, shieldRadius);
        }
    }
}
