using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Geneforge.Gameplay.Characters.Enemies.Suporte
{
    public class Suporte : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;

        [Header("Comportamento")]
        public bool stayStationary = true;
        public bool rotateInPlace = true;

        [Header("Wander Settings")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;
        public float idleWaitDuration = 1f;

        [Header("Deteção / Mirar")]
        public float detectionRadius = 20f;
        public float attackRange = 10f;
        public float attackRate = 1f; // ritmo das animações Attack/AttackB/AttackC

        [Header("Escudo")]
        public float shieldDuration = 5f;
        public float shieldRadius = 8f;
        public int maxAlliesPerCast = 0;

        [Header("Visual do Círculo")]
        public Vector3 indicatorOffset = new Vector3(0f, 2.0f, 0f);
        public float circleRadius = 0.5f;
        public int circleSegments = 24;
        public float circleWidth = 0.05f;

        [Header("Layer (Opcional)")]
        public int shieldedLayer = -1;

        [Header("Feedback a Dano")]
        public float damagePauseDuration = 0.5f;

        // --- Estado interno ---
        Vector3 spawnPos, wanderTarget;
        float wanderTimer, lastAttackTime;
        bool isIdleWaiting = false;
        float idleWaitTimer = 0f;

        enum State { Wandering, Attacking }
        State state = State.Wandering;

        Transform player;
        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        static readonly Collider[] overlapBuffer = new Collider[96];
        int enemyLayer = -1;

        class ShieldData
        {
            public Transform target;
            public float endTime;
            public int originalLayer = -1;
            public List<Collider> colliders = new List<Collider>();
            public List<bool> prevEnabled = new List<bool>();
            public GameObject circleObj;
            public LineRenderer lr;
        }

        readonly Dictionary<Transform, ShieldData> activeShields = new Dictionary<Transform, ShieldData>();

        void Start()
        {
            spawnPos = transform.position;
            PickWanderTarget();

            player = GameObject.FindWithTag("Player")?.transform;

            var enemy = GetComponent<Enemy>();
            if (enemy != null)
                enemy.OnDamaged += HandleOnDamaged;

            enemyLayer = LayerMask.NameToLayer("Enemy");
        }

        void OnDestroy()
        {
            var enemy = GetComponent<Enemy>();
            if (enemy != null)
                enemy.OnDamaged -= HandleOnDamaged;
        }

        void HandleOnDamaged(float dmg)
        {
            if (!isDamagePaused)
            {
                isDamagePaused = true;
                damagePauseTimer = 0f;
            }
        }

        void Update()
        {
            UpdateActiveShields();

            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                if (animator) animator.SetFloat("Speed", 0f);
                if (damagePauseTimer >= damagePauseDuration) isDamagePaused = false;
                return;
            }

            float distToPlayer = (player != null) ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
            state = (distToPlayer <= attackRange) ? State.Attacking : State.Wandering;

            if (state == State.Wandering && !stayStationary)
            {
                WanderMove();
            }
            else
            {
                if (animator) animator.SetFloat("Speed", 0f);
            }

            if (state == State.Attacking && rotateInPlace && player != null)
            {
                Vector3 face = player.position - transform.position; face.y = 0f;
                if (face.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(face.normalized);
            }

            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;
                float roll = Random.value;
                if (roll < 0.6f) animator?.SetTrigger("Attack");
                else if (roll < 0.85f) animator?.SetTrigger("AttackB");
                else animator?.SetTrigger("AttackC");
            }
        }

        // -------- ESCUDO: chamado via evento da animação ----------
        public void OnAttackHit()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, shieldRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);

            List<Transform> candidates = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (!col) continue;
                var root = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
                if (root == this.transform) continue;
                var enemy = root.GetComponentInParent<Enemy>();
                if (enemy == null) continue;
                if (!candidates.Contains(enemy.transform))
                    candidates.Add(enemy.transform);
            }

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
                    if (maxAlliesPerCast > 0 && applied >= maxAlliesPerCast) break;
                }
            }
        }

        bool ApplyShieldToTarget(Transform target)
        {
            if (!target) return false;

            ShieldData data;
            if (!activeShields.TryGetValue(target, out data))
            {
                data = new ShieldData { target = target };

                // guardar e desligar colliders
                data.colliders.Clear();
                target.GetComponentsInChildren(true, data.colliders);
                data.prevEnabled.Clear();
                foreach (var c in data.colliders)
                {
                    if (!c) { data.prevEnabled.Add(false); continue; }
                    data.prevEnabled.Add(c.enabled);
                    c.enabled = false;
                }

                // mudar layer se configurado
                if (shieldedLayer >= 0)
                {
                    data.originalLayer = target.gameObject.layer;
                    SetLayerRecursively(target.gameObject, shieldedLayer);
                }

                // criar círculo
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

        void UpdateActiveShields()
        {
            if (activeShields.Count == 0) return;

            var toRemove = new List<Transform>();

            foreach (var kv in activeShields)
            {
                var data = kv.Value;
                if (!data.target) { toRemove.Add(kv.Key); continue; }

                // atualizar posição e círculo
                if (data.circleObj && data.lr)
                {
                    Vector3 center = data.target.position + indicatorOffset;
                    for (int i = 0; i < circleSegments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / circleSegments;
                        float x = Mathf.Cos(angle) * circleRadius;
                        float z = Mathf.Sin(angle) * circleRadius;
                        data.lr.SetPosition(i, center + new Vector3(x, 0f, z));
                    }
                }

                if (Time.time >= data.endTime)
                {
                    // reverter colliders
                    int idx = 0;
                    foreach (var c in data.colliders)
                    {
                        if (!c) { idx++; continue; }
                        bool prev = (idx < data.prevEnabled.Count) ? data.prevEnabled[idx] : true;
                        c.enabled = prev;
                        idx++;
                    }

                    // reverter layer
                    if (data.originalLayer >= 0)
                        SetLayerRecursively(data.target.gameObject, data.originalLayer);

                    if (data.circleObj) Destroy(data.circleObj);

                    toRemove.Add(kv.Key);
                }
            }

            foreach (var t in toRemove) activeShields.Remove(t);
        }

        // -------- Wander ----------
        void WanderMove()
        {
            Vector3 targetPos = wanderTarget;
            float speed = isIdleWaiting ? 0f : wanderSpeed;

            if (speed > 0f)
            {
                Vector3 dir = targetPos - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.position += dir.normalized * speed * Time.deltaTime;
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            if (animator) animator.SetFloat("Speed", Mathf.Clamp01(speed / Mathf.Max(0.01f, wanderSpeed)));

            if (!isIdleWaiting)
            {
                wanderTimer += Time.deltaTime;
                if (wanderTimer >= wanderInterval || Vector3.Distance(transform.position, wanderTarget) < 0.2f)
                {
                    isIdleWaiting = true; idleWaitTimer = 0f;
                }
            }
            else
            {
                idleWaitTimer += Time.deltaTime;
                if (idleWaitTimer >= idleWaitDuration)
                {
                    isIdleWaiting = false; wanderTimer = 0f; PickWanderTarget();
                }
            }
        }

        void PickWanderTarget()
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, shieldRadius);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        static void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (!obj) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
                if (child) SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
