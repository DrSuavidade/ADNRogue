using UnityEngine;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public PlayerHealth playerHealth;

    [Header("Wander Settings")]
    public float wanderRadius     = 5f;
    public float wanderInterval   = 3f;
    public float wanderSpeed      = 2f;
    public float idleWaitDuration = 1f;

    [Header("Chase & Attack")]
    public float detectionRadius = 20f;
    public float chaseSpeed      = 4f;    
    public float attackRange     = 1.5f;
    public float attackRate      = 1f;
    public float damagePerHit    = 10f;

    [Header("Damage Pause")]
    [Tooltip("Seconds to freeze AI when damaged")]
    public float damagePauseDuration = 0.5f; 

    Vector3 spawnPos;
    Vector3 wanderTarget;
    float wanderTimer;
    float lastAttackTime;

    enum State { Wandering, Chasing, Attacking }
    State state = State.Wandering;

    Transform player;
    float currentSpeed;
    bool isIdleWaiting    = false;
    float idleWaitTimer   = 0f;

    // *** New pause fields ***
    bool  isDamagePaused  = false;
    float damagePauseTimer = 0f;

    void Start()
    {
        spawnPos = transform.position;
        PickWanderTarget();

        player = GameObject.FindWithTag("Player")?.transform;
        if (player != null && playerHealth == null)
            playerHealth = player.GetComponent<PlayerHealth>();

        // Subscribe to damage event
        var enemy = GetComponent<Enemy>();
        if (enemy != null)
            enemy.OnDamaged += HandleOnDamaged;
    }

    void OnDestroy()
    {
        // Unsubscribe
        var enemy = GetComponent<Enemy>();
        if (enemy != null)
            enemy.OnDamaged -= HandleOnDamaged;
    }

    void HandleOnDamaged(float dmg)
    {
        // Only trigger a pause if not already paused
        if (!isDamagePaused)
        {
            isDamagePaused   = true;
            damagePauseTimer = 0f;
        }
    }

    void Update()
    {
        // 1) Handle damage pause
        if (isDamagePaused)
        {
            damagePauseTimer += Time.deltaTime;
            // keep animation idle
            if (animator != null)
                animator.SetFloat("Speed", 0f);

            if (damagePauseTimer >= damagePauseDuration)
                isDamagePaused = false;

            return;
        }

        if (player == null) return;

        // 2) Determine state
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange)         state = State.Attacking;
        else if (dist <= detectionRadius) state = State.Chasing;
        else                              state = State.Wandering;

        // 3) Decide raw targetSpeed
        float targetSpeed;
        Vector3 targetPos;
        switch (state)
        {
            case State.Chasing:
                targetPos   = player.position;
                targetSpeed = chaseSpeed;
                break;
            case State.Wandering:
                targetPos   = wanderTarget;
                targetSpeed = isIdleWaiting ? 0f : wanderSpeed;
                break;
            default:
                targetPos   = transform.position;
                targetSpeed = 0f;
                break;
        }

        // 4) Move & rotate
        currentSpeed = targetSpeed; // no smoothing here; instant for clarity
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

        // 5) Animate
        float normSpeed = chaseSpeed > 0f
            ? Mathf.Clamp01(currentSpeed / chaseSpeed)
            : 0f;
        if (animator != null)
            animator.SetFloat("Speed", normSpeed);

        // 6) Attack timing
        if (state == State.Attacking && Time.time >= lastAttackTime + attackRate)
        {
            lastAttackTime = Time.time;
            animator.SetTrigger("Attack");
        }

        // 7) Wander + idle logic
        if (state == State.Wandering)
        {
            if (!isIdleWaiting)
            {
                wanderTimer += Time.deltaTime;
                if (wanderTimer >= wanderInterval ||
                    Vector3.Distance(transform.position, wanderTarget) < 0.2f)
                {
                    isIdleWaiting  = true;
                    idleWaitTimer  = 0f;
                }
            }
            else
            {
                idleWaitTimer += Time.deltaTime;
                if (idleWaitTimer >= idleWaitDuration)
                {
                    isIdleWaiting = false;
                    wanderTimer   = 0f;
                    PickWanderTarget();
                }
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;  Gizmos.DrawWireSphere(transform.position, wanderRadius);
        Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;     Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
