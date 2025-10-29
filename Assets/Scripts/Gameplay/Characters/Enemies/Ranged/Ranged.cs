using UnityEngine;
using System.Collections;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Ranged

{
    /// <summary>
    /// AI idêntica ao EnemyAI (Wander → Chase → Attack) mas com ataque à distância (lança).
    /// NÃO cria parâmetros novos no Animator. Só usa:
    ///  - Trigger "Attack"
    ///  - Float   "Speed"
    /// (Damaged/Death continuam a ser tratados pelo Enemy.cs)
    ///
    /// A animação deve ter os eventos:
    ///  - OnThrowRelease()  → solta a lança
    ///  - OnAttackHit()     → opcional; aqui é um stub (não faz nada)
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public class Ranged : MonoBehaviour
    {
        [Header("Referências")]
        public Animator animator;                // usa "Attack" e "Speed" apenas
        public PlayerHealth playerHealth;        // opcional (projetil trata do dano)

        [Header("Comportamento")]
        public bool stayStationary = false;
        public bool rotateInPlace = true;

        [Header("Wander")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;
        public float idleWaitDuration = 1f;

        [Header("Perceção / Ataque")]
        public float detectionRadius = 20f;
        public float chaseSpeed = 4f;
        public float attackRange = 12f;
        public float attackRate = 1.25f;

        [Header("Direção do lançamento")]
        public bool requireLineOfSight = true;
        public LayerMask lineOfSightMask = ~0;
        public float lineOfSightPadding = 0.1f;

        [Header("Lança (embutida)")]
        [Tooltip("Prefab da lança (tem Rigidbody + Collider + SpearProjectile).")]
        public GameObject spearPrefab;
        [Tooltip("Empty na mão onde a lança fica presa.")]
        public Transform spearSocket;
        [Tooltip("Empty à frente da mão para a direção. Se vazio, usa o socket/transform.")]
        public Transform throwOrigin;
        [Tooltip("Força/velocidade inicial do lançamento.")]
        public float throwForce = 30f;
        [Tooltip("Ajuste fino da mira (ex.: um pouco acima do alvo).")]
        public Vector3 aimOffset = Vector3.zero;

        [Header("Pausa ao sofrer dano")]
        public float damagePauseDuration = 0.5f;

        // ----------------- estado interno -----------------
        Vector3 spawnPos;
        Vector3 wanderTarget;
        float wanderTimer;
        float lastAttackTime;

        enum State { Wandering, Chasing, Attacking }
        State state = State.Wandering;

        Transform player;
        float currentSpeed;
        bool isIdleWaiting = false;
        float idleWaitTimer = 0f;

        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        GameObject heldSpear;
        Collider[] ownerCols;

        void Start()
        {
            spawnPos = transform.position;
            PickWanderTarget();

            player = GameObject.FindWithTag("Player")?.transform;
            if (player != null && playerHealth == null)
                playerHealth = player.GetComponent<PlayerHealth>();

            // pausa breve quando sofre dano (evento do Enemy.cs)
            var enemy = GetComponent<Enemy>();
            if (enemy != null) enemy.OnDamaged += HandleOnDamaged;

            ownerCols = GetComponentsInChildren<Collider>(true);
            SpawnHeldSpear();
        }

        void OnDestroy()
        {
            var enemy = GetComponent<Enemy>();
            if (enemy != null) enemy.OnDamaged -= HandleOnDamaged;

            CancelInvoke(nameof(SpawnHeldSpear));
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
            // 1) pausa por dano
            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                if (animator) animator.SetFloat("Speed", 0f);
                if (damagePauseTimer >= damagePauseDuration) isDamagePaused = false;
                return;
            }

            // After the damage-pause return, before using 'dist' to pick state:
            if (A_ChameleonCamouflage.InvisibleActive)
            {
                // Behave as if the player isn't there
                state = State.Wandering;
                currentSpeed = 0f;

                // Idle animation (or continue wandering if you prefer)
                if (animator != null) animator.SetFloat("Speed", 0f);

                // Skip chase/attack for this frame
                return;
            }

            if (!player) return;

            // 2) estado
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= attackRange && (!requireLineOfSight || HasLineOfSight()))
                state = State.Attacking;
            else if (!stayStationary && dist <= detectionRadius)
                state = State.Chasing;
            else
                state = State.Wandering;

            // 3) movimento
            float targetSpeed;
            Vector3 targetPos;

            switch (state)
            {
                case State.Chasing:
                    if (stayStationary) { targetPos = transform.position; targetSpeed = 0f; }
                    else { targetPos = player.position; targetSpeed = chaseSpeed; }
                    break;

                case State.Wandering:
                    if (stayStationary) { targetPos = transform.position; targetSpeed = 0f; }
                    else { targetPos = wanderTarget; targetSpeed = isIdleWaiting ? 0f : wanderSpeed; }
                    break;

                default: // Attacking
                    targetPos = transform.position; targetSpeed = 0f;
                    break;
            }

            currentSpeed = targetSpeed;
            if (currentSpeed > 0f)
            {
                Vector3 dir = targetPos - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.position += dir.normalized * currentSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            // 4) olhar para o alvo (em ataque ou se quiseres rodar parado)
            if ((state == State.Attacking || rotateInPlace) && player != null)
            {
                Vector3 face = player.position - transform.position; face.y = 0f;
                if (face.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(face.normalized);
            }

            // 5) locomotion -> só "Speed"
            if (animator)
            {
                float normSpeed = chaseSpeed > 0f ? Mathf.Clamp01(currentSpeed / chaseSpeed) : 0f;
                animator.SetFloat("Speed", normSpeed);
            }

            // 6) ataque → só usa Trigger "Attack"
            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;

                if (heldSpear != null && animator)
                    animator.SetTrigger("Attack"); // a clip chamará OnThrowRelease()
            }

            // 7) wander
            if (!stayStationary && state == State.Wandering)
            {
                if (!isIdleWaiting)
                {
                    wanderTimer += Time.deltaTime;
                    if (wanderTimer >= wanderInterval ||
                        Vector3.Distance(transform.position, wanderTarget) < 0.2f)
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
        }

        // ============================
        //    Lança (embutida)
        // ============================
        void SpawnHeldSpear()
        {
            if (!spearPrefab || !spearSocket)
            {
                Debug.LogError("[Ranged] spearPrefab/spearSocket em falta.");
                return;
            }

            // limpar o socket (evita duplicações ao rearmar)
            for (int i = spearSocket.childCount - 1; i >= 0; i--)
                Destroy(spearSocket.GetChild(i).gameObject);

            if (heldSpear != null) return;

            heldSpear = Instantiate(spearPrefab, spearSocket);
            heldSpear.name = "Spear_Held";
            heldSpear.transform.localPosition = Vector3.zero;
            heldSpear.transform.localRotation = Quaternion.identity;

            var rb = heldSpear.GetComponent<Rigidbody>();
            var col = heldSpear.GetComponent<Collider>();
            var proj = heldSpear.GetComponent<SpearProjectile>();
            if (!rb || !col || !proj)
            {
                Debug.LogError("[Ranged] O prefab da lança precisa de Rigidbody + Collider + SpearProjectile.");
                return;
            }

            rb.isKinematic = true; // presa na mão
            col.enabled = false;   // sem colisão na mão
        }

        // ===== Animation Events =====
        // Evento na clip "Attack" — frame em que a mão solta a lança
        public void OnThrowRelease()
        {
            ThrowNow();
        }

        // Evento herdado do melee — aqui é neutro (para não dar erro na clip)
        public void OnAttackHit() { /* sem efeito no ranged */ }

        void ThrowNow()
        {
            if (heldSpear == null) return;

            var proj = heldSpear.GetComponent<SpearProjectile>();
            var rb = heldSpear.GetComponent<Rigidbody>();
            var col = heldSpear.GetComponent<Collider>();

            if (!proj || !rb || !col)
            {
                Debug.LogError("[Ranged] SpearProjectile/Rigidbody/Collider em falta no spearPrefab.");
                return;
            }

            // soltar da mão
            heldSpear.transform.SetParent(null, true);

            // direção
            Vector3 originPos = (throwOrigin ? throwOrigin.position :
                                (spearSocket ? spearSocket.position : transform.position));
            Vector3 targetPos = (player ? player.position : transform.position + transform.forward * 5f) + aimOffset;
            Vector3 dir = (targetPos - originPos).normalized;

            // lançar (o SpearProjectile ativa física e ignora colisões do dono)
            if (ownerCols == null || ownerCols.Length == 0)
                ownerCols = GetComponentsInChildren<Collider>(true);

            proj.Launch(dir * throwForce, ownerCols);

            heldSpear = null;

            // rearmar após curto atraso
            Invoke(nameof(SpawnHeldSpear), 0.4f);
        }

        // ============================
        //    Auxiliares
        // ============================
        bool HasLineOfSight()
        {
            if (!requireLineOfSight || player == null) return true;

            Vector3 origin = (throwOrigin ? throwOrigin.position :
                             (spearSocket ? spearSocket.position : transform.position));
            Vector3 target = player.position + aimOffset;

            Vector3 diff = target - origin;
            float dist = Mathf.Max(0f, diff.magnitude - lineOfSightPadding);
            if (dist <= 0.001f) return true;

            Vector3 dir = diff / (diff.magnitude > 0.0001f ? diff.magnitude : 1f);

            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider && hit.collider.transform != player && !hit.collider.transform.IsChildOf(player))
                    return false;
            }
            return true;
        }

        void PickWanderTarget()
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void OnDrawGizmosSelected()
        {
            if (!stayStationary)
            {
                Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, wanderRadius);
                Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);

            if (throwOrigin)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(throwOrigin.position, throwOrigin.position + throwOrigin.forward * 2f);
            }
        }
    }
}
