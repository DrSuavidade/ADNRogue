using UnityEngine;
using System;
using System.Collections;
using System.Linq;

public class Enemy : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 5f;
    private float currentHealth;

    [Header("Animation")]
    public Animator animator;                // assign your Animator
    public string damagedTrigger = "Damaged";
    public string deathTrigger = "Death";
    [Tooltip("Length of the death animation clip in seconds")]
    public float deathAnimDuration = 1f;
    [Tooltip("Optional fade‐out time after death anim")]

    // Expose health
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    // Fired the first time the enemy is damaged
    public event Action OnFirstHit;

    public event Action<float> OnDamaged;

    private bool hasBeenHit = false;
    private bool isDead = false;

    [Header("Damage UI")]
    public GameObject damageTextPrefab;  // assign your DamageText prefab
    public Vector3 damageTextOffset = new Vector3(0, 2.5f, 0);

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float dmg, bool wasCrit = false)
    {
        if (dmg <= 0f || isDead) return;

        if (!hasBeenHit)
        {
            hasBeenHit = true;
            OnFirstHit?.Invoke();
        }

        currentHealth = Mathf.Max(0f, currentHealth - dmg);
        Debug.Log($"{name} took {dmg} damage, remaining HP: {currentHealth}");

        if (damageTextPrefab != null)
        {
            Vector3 spawnPos = transform.position + damageTextOffset;
            var dtObj = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);
            var dt = dtObj.GetComponent<DamageText>();
            if (dt != null)
                dt.Initialize(dmg, wasCrit);
        }

        OnDamaged?.Invoke(dmg);

        if (currentHealth > 0f)
        {
            if (animator != null)
                animator.SetTrigger(damagedTrigger);
        }
        else
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1) Disable everything that can still move or hit
        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 2) Trigger death animation
        if (animator != null)
            animator.SetTrigger(deathTrigger);

        // 3) Wait for it to finish, then destroy
        Destroy(gameObject, deathAnimDuration);
    }


    // NEW: Smooth knockback slide
    public void ApplyKnockback(Vector3 direction, float force, float duration = 0.1f)
    {
        // displacement = 0.1f * force
        float disp = 0.1f * force;
        Vector3 targetPos = transform.position + direction.normalized * disp;
        StopAllCoroutines();
        StartCoroutine(KnockbackRoutine(transform.position, targetPos, duration));
    }

    IEnumerator KnockbackRoutine(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = to;
    }
}
