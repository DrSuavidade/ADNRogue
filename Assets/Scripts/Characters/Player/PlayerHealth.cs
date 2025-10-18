using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Revive Settings")]
    public Transform respawnPoint;          // assign an empty GameObject in-scene
    public float invulnerableDuration = 2f; // seconds after revive
    public float postReviveHPPercent = 1f;  // 1 = full HP, 0.5 = half HP

    RunStats runStats;
    bool isInvulnerable;
    float invulnEndTime;
    Animator animator;

    void Awake()
    {
        runStats = GetComponent<RunStats>();
        runStats.OnPlayerDeath += HandleDeath;
        animator = GetComponentInChildren<Animator>();
    }

    void OnDestroy()
    {
        runStats.OnPlayerDeath -= HandleDeath;
    }

    void Update()
    {
        if (isInvulnerable && Time.time >= invulnEndTime)
            isInvulnerable = false;
    }

    public void ApplyDamage(float dmg)
    {
        if (isInvulnerable) return;

        bool died = runStats.TakeDamage(dmg);
        // (You’d also update your health‐bar UI here)

        if (!died)
        {
            // Play damaged animation
            animator?.SetTrigger("Damaged");
        }
    }

    void HandleDeath()
    {
        // Consume a life
        runStats.lives--;

        if (runStats.lives > 0)
        {
            // Play death‐but‐revive animation
            animator?.SetTrigger("Death");

            // Immediately revive (you can add a slight delay if desired)
            RevivePlayer();
        }
        else
        {
            // Play final death animation
            animator?.SetTrigger("FinalDeath");

            // Disable further input / run‐over logic
            GameOver();
        }
    }

    void RevivePlayer()
    {
        // Reset HP (full or partial)
        float restore = runStats.maxHP * postReviveHPPercent;
        runStats.currentHP = restore;

        // Move to respawn point
        if (respawnPoint != null)
            transform.position = respawnPoint.position;

        // Temporary invulnerability
        isInvulnerable = true;
        invulnEndTime = Time.time + invulnerableDuration;

        // (Trigger UI update for lives and flash effect, etc.)
    }

    public void BeginInvulnerability(float duration)
    {
        isInvulnerable = true;
        invulnEndTime = Time.time + duration;
    }

    void GameOver()
    {
        // Your end‐of‐run logic: disable input, show summary screen, etc.
        Debug.Log("No lives left. Game Over!");
    }
}
