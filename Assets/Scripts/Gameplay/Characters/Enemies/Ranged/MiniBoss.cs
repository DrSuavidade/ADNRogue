using UnityEngine;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Weapons.Bullets;

namespace Geneforge.Gameplay.Characters.Enemies.Ranged
{
    [RequireComponent(typeof(Enemy))]
    public class MiniBoss : MonoBehaviour
    {
        public enum WeaponType { Projectile, Flamethrower }

        [Header("Referências")]
        public Animator animator;
        public PlayerHealth playerHealth;

        [Header("Animator Params")]
        public string attackTrigger = "Attack";
        public string hitTrigger = "Damaged";
        public string deathTrigger = "Death";
        public string dodgeLeftTrigger = "DodgeLeft";
        public string dodgeRightTrigger = "DodgeRight";

        [Header("Movimento / Perseguição")]
        public float detectionRadius = 20f;
        public float chaseSpeed = 4f;
        public float attackRange = 10f;
        public float attackRate = 1.25f;

        [Header("Arma - Projétil")]
        public GameObject bulletPrefab;
        public Transform[] firePoints;
        public float bulletSpeed = 30f;

        [Header("Arma - Flamethrower")]
        public WeaponType weaponType = WeaponType.Projectile;
        public float flameLength = 6f;
        public float flameWidth = 3f;
        public float flameHeight = 2f;
        public float flameDamage = 10f;
        public float flameForwardOffset = 0.5f;
        public float flameVerticalOffset = 0f;

        [Header("Danos / Root Motion")]
        public float damagePauseDuration = 0.5f;

        // ------------------------------
        // DODGE NORMAL
        // ------------------------------
        [Header("Dodge Config")]
        public float bulletDetectRadius = 7f;
        public float minBulletSpeed = 3f;
        public float dodgeSpeed = 8f;
        public float dodgeDistance = 3.5f;
        public float dodgeCooldown = 2.6f;
        public float dodgeChance = 0.35f;
        public bool debugDodge = true;

        float nextDodgeTime;


        // ------------------------------
        // DODGE ANTECIPADO (SEKIRO/NIOH)
        // ------------------------------
        [Header("Predictive Dodge (Sekiro/Nioh)")]
        public float predictiveDodgeChance = 0.25f;
        public float predictiveDodgeCooldown = 7.5f;
        public float predictiveMaxRange = 18f;
        public int predictiveDodgesPerCycle = 1;
        public float predictiveCycleReset = 10f;

        static Transform playerPreparingShot;
        float nextPredictiveDodgeTime = 0f;
        int predictiveDodgesUsed = 0;
        float predictiveCycleTimer = 0f;

        // Para o player enviar aviso
        public static void NotifyPlayerPreparingShot(Transform p)
        {
            playerPreparingShot = p;
        }

        // ------------------------------
        Enemy enemy;
        Transform player;

        enum State { Wandering, Chasing, Attacking, Dodging }
        State state = State.Wandering;

        float lastAttackTime;
        bool isDamagePaused = false;
        float damagePauseTimer = 0f;

        Vector3 dodgeDir;
        float dodgeTimer;
        float dodgeDuration;


        // ============================================================
        void Awake()
        {
            enemy = GetComponent<Enemy>();
        }

        void Start()
        {
            player = GameObject.FindWithTag("Player")?.transform;

            if (player != null && playerHealth == null)
                playerHealth = player.GetComponent<PlayerHealth>();

            if (enemy != null)
                enemy.OnDamaged += OnEnemyDamaged;

            animator.applyRootMotion = false;

            dodgeDuration = dodgeDistance / dodgeSpeed;
        }

        void OnDestroy()
        {
            if (enemy != null)
                enemy.OnDamaged -= OnEnemyDamaged;
        }

        // ============================================================
        void OnEnemyDamaged(float dmg)
        {
            isDamagePaused = true;
            damagePauseTimer = 0f;
            animator.SetTrigger(hitTrigger);
        }

        // ============================================================
        void Update()
        {
            if (!player) return;

            if (enemy.CurrentHealth <= 0)
            {
                animator.SetTrigger(deathTrigger);
                return;
            }

            if (isDamagePaused)
            {
                damagePauseTimer += Time.deltaTime;
                if (damagePauseTimer >= damagePauseDuration)
                    isDamagePaused = false;
                return;
            }

            // RESET ciclo de dodge antecipado
            predictiveCycleTimer += Time.deltaTime;
            if (predictiveCycleTimer >= predictiveCycleReset)
            {
                predictiveCycleTimer = 0f;
                predictiveDodgesUsed = 0;
            }

            float dist = Vector3.Distance(transform.position, player.position);

            // ============================================================
            // 1) DODGE ANTECIPADO (SEKIRO / NIOH)
            // ============================================================
            if (CanDoPredictiveDodge())
            {
                StartDodge(DecidePredictiveDodgeDirection());
                predictiveDodgesUsed++;
                nextPredictiveDodgeTime = Time.time + predictiveDodgeCooldown;
                return;
            }

            // ============================================================
            // 2) DODGE NORMAL
            // ============================================================
            if (state != State.Dodging && Time.time >= nextDodgeTime)
            {
                if (DetectIncomingBullet(out Vector3 dd))
                {
                    StartDodge(dd);
                    return;
                }
            }

            // ============================================================
            // STATES
            // ============================================================
            switch (state)
            {
                case State.Chasing: UpdateChasing(dist); break;
                case State.Attacking: UpdateAttacking(dist); break;
                case State.Wandering: UpdateWandering(dist); break;
                case State.Dodging: UpdateDodging(); break;
            }
        }

        // ============================================================
        // PREDICTIVE DODGE CHECK
        // ============================================================
        bool CanDoPredictiveDodge()
        {
            if (playerPreparingShot == null) return false;
            if (Time.time < nextPredictiveDodgeTime) return false;
            if (predictiveDodgesUsed >= predictiveDodgesPerCycle) return false;

            // Probabilidade
            if (Random.value > predictiveDodgeChance) return false;

            float dist = Vector3.Distance(transform.position, playerPreparingShot.position);
            if (dist > predictiveMaxRange) return false;

            // Jogador tem de estar a apontar mais ou menos para o boss
            Vector3 toBoss = (transform.position - playerPreparingShot.position).normalized;
            float dot = Vector3.Dot(playerPreparingShot.forward, toBoss);
            return dot > 0.65f;
        }

        Vector3 DecidePredictiveDodgeDirection()
        {
            return Random.value < 0.5f ? transform.right : -transform.right;
        }


        // ============================================================
        // DODGE NORMAL
        // ============================================================
        bool DetectIncomingBullet(out Vector3 outDir)
        {
            outDir = Vector3.zero;

            if (Random.value > dodgeChance)
                return false;

            Collider[] hits = Physics.OverlapSphere(transform.position, bulletDetectRadius);

            foreach (var c in hits)
            {
                BulletSimple b = c.GetComponent<BulletSimple>();
                if (!b) continue;

                Rigidbody rb = c.attachedRigidbody;
                if (!rb) continue;

                if (rb.linearVelocity.magnitude < minBulletSpeed)
                    continue;

                float dotRight = Vector3.Dot(rb.linearVelocity.normalized, transform.right);
                float dotLeft = Vector3.Dot(rb.linearVelocity.normalized, -transform.right);

                outDir = dotRight > dotLeft ? -transform.right : transform.right;

                if (debugDodge)
                    Debug.DrawRay(transform.position, outDir * 2.5f, Color.red);

                nextDodgeTime = Time.time + dodgeCooldown;
                return true;
            }

            return false;
        }

        // ============================================================
        void StartDodge(Vector3 dir)
        {
            dodgeDir = dir.normalized;
            dodgeTimer = 0f;

            if (Vector3.Dot(dir, transform.right) < 0)
                animator.SetTrigger(dodgeLeftTrigger);
            else
                animator.SetTrigger(dodgeRightTrigger);

            state = State.Dodging;
        }

        void UpdateDodging()
        {
            transform.position += dodgeDir * dodgeSpeed * Time.deltaTime;

            dodgeTimer += Time.deltaTime;
            if (dodgeTimer >= dodgeDuration)
                state = State.Chasing;
        }

        // ============================================================
        void UpdateChasing(float dist)
        {
            if (dist <= attackRange)
            {
                state = State.Attacking;
                return;
            }

            Vector3 dir = (player.position - transform.position).normalized;
            dir.y = 0;

            transform.rotation = Quaternion.LookRotation(dir);
            transform.position += dir * chaseSpeed * Time.deltaTime;

            animator.SetFloat("Speed", chaseSpeed);
        }

        void UpdateAttacking(float dist)
        {
            animator.SetFloat("Speed", 0f);

            if (dist > attackRange)
            {
                state = State.Chasing;
                return;
            }

            Vector3 dir = player.position - transform.position;
            dir.y = 0;
            transform.rotation = Quaternion.LookRotation(dir);

            if (Time.time >= lastAttackTime + attackRate)
            {
                lastAttackTime = Time.time;
                animator.SetTrigger(attackTrigger);
            }
        }

        void UpdateWandering(float dist)
        {
            animator.SetFloat("Speed", 0f);

            if (dist <= detectionRadius)
                state = State.Chasing;
        }

        // ============================================================
        // SHOOT (ANIMATION EVENT)
        // ============================================================
        public void OnThrowRelease()
        {
            if (bulletPrefab == null || firePoints == null || firePoints.Length == 0)
                return;

            Transform fp = firePoints[0];
            Vector3 direction = (player.position - fp.position).normalized;

            GameObject b = Instantiate(bulletPrefab, fp.position, Quaternion.LookRotation(direction));
            Rigidbody rb = b.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.linearVelocity = direction * bulletSpeed;
            }
        }

        // ============================================================
        public void flamefx()
        {
            if (weaponType != WeaponType.Flamethrower) return;
            if (!player || !playerHealth) return;

            Vector3 fwd = transform.forward;
            Vector3 flat = new Vector3(fwd.x, 0f, fwd.z).normalized;
            Vector3 half = new Vector3(flameWidth * 0.5f, flameHeight * 0.5f, flameLength * 0.5f);

            Vector3 center =
                transform.position +
                flat * (flameLength * 0.5f + flameForwardOffset) +
                new Vector3(0, flameVerticalOffset + half.y, 0);

            Collider[] hits = Physics.OverlapBox(center, half, Quaternion.LookRotation(flat));

            foreach (var h in hits)
                if (h != null && (h.transform == player || h.transform.IsChildOf(player)))
                    playerHealth.ApplyDamage(flameDamage);
        }

        // ============================================================
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, bulletDetectRadius);
        }
    }
}
