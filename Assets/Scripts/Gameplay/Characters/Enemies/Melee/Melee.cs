using UnityEngine;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Melee
{
    public class Melee : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public PlayerHealth playerHealth;

        [Header("Behavior")]
        public bool stayStationary = false;
        public bool rotateInPlace = true;

        [Header("Wander Settings")]
        public float wanderRadius = 5f;
        public float wanderInterval = 3f;
        public float wanderSpeed = 2f;

        [Header("Chase & Attack")]
        public float detectionRadius = 20f;
        public float chaseSpeed = 4f;
        public float attackRange = 1.5f;
        public float attackRate = 1f;
        public float damagePerHit = 10f;

        [Header("Ataques Disponíveis")]
        [Tooltip("1 = só Attack | 2 = Attack + AttackB | 3 = Attack + AttackB + AttackC")]
        [Range(1, 3)]
        public int attackVariants = 3;

        [Header("Damage Pause")]
        public float damagePauseDuration = 0.5f;

        Vector3 spawnPos;
        Vector3 wanderTarget;
        float wanderTimer;
        float lastAttackTime;

        enum State { Wandering, Chasing, Attacking }
        State state = State.Wandering;

        Transform player;
        float currentSpeed;

        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        Geneforge.Gameplay.Characters.Enemies.Enemy enemy;
        Rigidbody _rb;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        void Start()
        {
            spawnPos = transform.position;
            PickWanderTarget();

            player = GameObject.FindWithTag("Player")?.transform;
            if (player != null && playerHealth == null)
                playerHealth = player.GetComponent<PlayerHealth>();

            enemy = GetComponent<Geneforge.Gameplay.Characters.Enemies.Enemy>();
            if (enemy != null)
                enemy.OnDamaged += HandleOnDamaged;
        }

        void OnDestroy()
        {
            if (enemy != null)
                enemy.OnDamaged -= HandleOnDamaged;
        }

        void HandleOnDamaged(float dmg)
        {
            isDamagePaused = true;
            damagePauseTimer = 0f;
        }

        void Update()
        {
            if (enemy != null && enemy.CurrentHealth <= 0f)
            {
                AnimatorSpeed(0f);
                HardStopNow();
                return;
            }

            if (player == null) return;

            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
            }

            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= attackRange) state = State.Attacking;
            else if (!stayStationary && dist <= detectionRadius) state = State.Chasing;
            else state = State.Wandering;

            Vector3 targetPos = transform.position;
            float targetSpeed = 0f;

            if (state == State.Chasing && !stayStationary)
            {
                targetPos = player.position;
                targetSpeed = chaseSpeed;
            }
            else if (state == State.Wandering && !stayStationary)
            {
                targetPos = wanderTarget;
                targetSpeed = wanderSpeed;
            }

            currentSpeed = targetSpeed;

            if (currentSpeed > 0f)
            {
                Vector3 dir = targetPos - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.position += dir.normalized * currentSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            AnimatorSpeed(currentSpeed / chaseSpeed);

            // ATAQUE COM VARIANTES
            if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;
                float roll = Random.value;

                if (attackVariants <= 1)
                    animator.SetTrigger("Attack");
                else if (attackVariants == 2)
                    animator.SetTrigger(roll < 0.6f ? "Attack" : "AttackB");
                else
                {
                    if (roll < 0.6f) animator.SetTrigger("Attack");
                    else if (roll < 0.85f) animator.SetTrigger("AttackB");
                    else animator.SetTrigger("AttackC");
                }
            }

            if (!stayStationary && state == State.Wandering)
            {
                wanderTimer += Time.deltaTime;
                if (wanderTimer >= wanderInterval)
                {
                    wanderTimer = 0f;
                    PickWanderTarget();
                }
            }
        }

        public void OnAttackHit()
        {
            if (playerHealth != null)
                playerHealth.ApplyDamage(damagePerHit);
        }

        void PickWanderTarget()
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
        }

        void AnimatorSpeed(float v)
        {
            if (animator != null)
                animator.SetFloat("Speed", v);
        }

        void HardStopNow()
        {
            if (animator) animator.SetFloat("Speed", 0f);
            if (_rb && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, wanderRadius);
            Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
