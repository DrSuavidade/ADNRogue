using UnityEngine;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public PlayerHealth playerHealth;

    [Header("Behavior")]
    [Tooltip("When enabled, this enemy never moves (no wander, no chase). It can still attack if you enter its attack range.")]
    public bool stayStationary = false;

    [Tooltip("When stationary, rotate to face the player while attacking.")]
    public bool rotateInPlace = true;

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

    [Header("Knockback Target (Optional)")]
    [Tooltip("If set, knockback will be applied relative to this transform (e.g., the object that has the CharacterController). Leave empty to auto-resolve via Player hierarchy.")]
    public Transform knockbackRootOverride;

    [Header("Knockback")]
    public bool applyKnockback = true;
    public float knockbackForce = 6f;
    public float knockbackUpward = 0.5f;
    public float knockbackMaxSpeed = 12f;

    // When pushing CharacterController directly (no Rigidbody/Receiver found)
    [Tooltip("How long to apply CC-based knockback if no Rigidbody/Receiver is found.")]
    public float ccKnockbackDuration = 0.25f;
    [Tooltip("How quickly CC knockback decays back to zero.")]
    public float ccKnockbackDecay = 8f;


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

    // Damage pause fields
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
        var enemy = GetComponent<Enemy>();
        if (enemy != null)
            enemy.OnDamaged -= HandleOnDamaged;
    }

    void HandleOnDamaged(float dmg)
    {
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
            if (animator != null)
                animator.SetFloat("Speed", 0f);

            if (damagePauseTimer >= damagePauseDuration)
                isDamagePaused = false;

            return;
        }

        if (player == null) return;

        // 2) Determine state
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= attackRange)
        {
            state = State.Attacking;
        }
        else if (!stayStationary && dist <= detectionRadius)
        {
            state = State.Chasing;
        }
        else
        {
            // If stationary, we do not wander; otherwise we wander as normal
            state = State.Wandering;
        }

        // 3) Decide raw targetSpeed and targetPos
        float targetSpeed;
        Vector3 targetPos;

        switch (state)
        {
            case State.Chasing:
                if (stayStationary)
                {
                    // Do not move when stationary
                    targetPos   = transform.position;
                    targetSpeed = 0f;
                }
                else
                {
                    targetPos   = player.position;
                    targetSpeed = chaseSpeed;
                }
                break;

            case State.Wandering:
                if (stayStationary)
                {
                    // No wandering when stationary
                    targetPos   = transform.position;
                    targetSpeed = 0f;
                }
                else
                {
                    targetPos   = wanderTarget;
                    targetSpeed = isIdleWaiting ? 0f : wanderSpeed;
                }
                break;

            default: // Attacking
                targetPos   = transform.position;
                targetSpeed = 0f;
                break;
        }

        // 4) Move
        currentSpeed = targetSpeed; // instant for clarity
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

        // 4b) Optional rotate in place while attacking (no movement)
        if (state == State.Attacking && rotateInPlace && player != null)
        {
            Vector3 face = player.position - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(face.normalized);
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

        // 7) Wander + idle logic (only when not stationary)
        if (!stayStationary && state == State.Wandering)
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

        if (!applyKnockback || player == null) return;

        // 1) Compute impulse
        Vector3 dir = (player.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        Vector3 impulse = dir * knockbackForce + Vector3.up * knockbackUpward;

        // 2) Choose the best root to apply knockback to
        Transform root = knockbackRootOverride != null ? knockbackRootOverride : player;

        // 3) Try Receiver anywhere in hierarchy
        var receiver =
            root.GetComponentInChildren<KnockbackReceiver>() ??
            root.GetComponentInParent<KnockbackReceiver>();

        if (receiver != null)
        {
            receiver.ApplyImpulse(impulse);
            return;
        }

        // 4) Try Rigidbody anywhere in hierarchy
        var rb =
            root.GetComponentInChildren<Rigidbody>() ??
            root.GetComponentInParent<Rigidbody>();

        if (rb != null)
        {
            rb.AddForce(impulse, ForceMode.Impulse);

            if (knockbackMaxSpeed > 0f)
            {
                Vector3 v = rb.linearVelocity;
                Vector3 horiz = new Vector3(v.x, 0f, v.z);
                if (horiz.magnitude > knockbackMaxSpeed)
                {
                    horiz = horiz.normalized * knockbackMaxSpeed;
                    rb.linearVelocity = new Vector3(horiz.x, v.y, horiz.z);
                }
            }
            return;
        }

        // 5) As a universal fallback: push CharacterController via cc.Move over a few frames
        var cc =
            root.GetComponentInChildren<CharacterController>() ??
            root.GetComponentInParent<CharacterController>();

        if (cc != null && cc.enabled)
        {
            StopCoroutine(nameof(CCKnockbackRoutine)); // ensure single instance
            StartCoroutine(CCKnockbackRoutine(cc, impulse, ccKnockbackDuration, ccKnockbackDecay));
        }
    }

    // Decaying CC push that plays nicely with PlayerController's own cc.Move()
    IEnumerator CCKnockbackRoutine(CharacterController cc, Vector3 impulse, float duration, float decayRate)
    {
        float t = 0f;
        Vector3 vel = impulse; // treat impulse as instantaneous velocity

        while (t < duration && cc != null && cc.enabled)
        {
            // Move additively (CharacterController sums multiple Move calls per frame)
            cc.Move(vel * Time.deltaTime);

            // Exponential decay
            float k = Mathf.Clamp01(decayRate * Time.deltaTime);
            vel = Vector3.Lerp(vel, Vector3.zero, k);

            t += Time.deltaTime;
            yield return null;
        }
    }


    void PickWanderTarget()
    {
        Vector2 rnd = Random.insideUnitCircle * wanderRadius;
        wanderTarget = spawnPos + new Vector3(rnd.x, 0f, rnd.y);
    }

    void OnDrawGizmosSelected()
    {
        // Only draw wander/detection when not stationary to avoid confusion in editor
        if (!stayStationary)
        {
            Gizmos.color = Color.yellow;  Gizmos.DrawWireSphere(transform.position, wanderRadius);
            Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
        Gizmos.color = Color.red;     Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
