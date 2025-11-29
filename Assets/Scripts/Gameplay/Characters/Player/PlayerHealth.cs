using UnityEngine;
using Geneforge.Core.Stats;

namespace Geneforge.Gameplay.Characters.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Revive Settings")]
        [SerializeField] Transform respawnPoint;          // assign an empty GameObject in-scene
        [SerializeField] float invulnerableDuration = 2f; // seconds after revive
        [SerializeField, Range(0f, 1f)]
        float postReviveHPPercent = 1f;  // 1 = full HP, 0.5 = half HP

        RunStats runStats;
        bool isInvulnerable;
        float invulnEndTime;
        Animator animator;

        void Awake()
        {
            runStats = GetComponent<RunStats>();
            if (runStats == null)
            {
                Debug.LogError("PlayerHealth requires a RunStats component on the same GameObject.", this);
                enabled = false;
                return;
            }

            runStats.OnPlayerDeath += HandleDeath;
            animator = GetComponentInChildren<Animator>();
        }

        void OnDestroy()
        {
            if (runStats != null)
                runStats.OnPlayerDeath -= HandleDeath;
        }

        void Update()
        {
            if (isInvulnerable && Time.time >= invulnEndTime)
                isInvulnerable = false;
        }

        public void ApplyDamage(float dmg)
        {
            if (!enabled || runStats == null) return;
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
            if (runStats == null) return;

            // Consume a life
            runStats.Lives--;

            if (runStats.Lives > 0)
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
            if (runStats == null) return;

            // Reset HP (full or partial)
            float targetHP = runStats.MaxHP * Mathf.Clamp01(postReviveHPPercent);

            // CurrentHP is 0 when OnPlayerDeath fired, so Heal() will set it to targetHP
            runStats.Heal(targetHP);

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
}
