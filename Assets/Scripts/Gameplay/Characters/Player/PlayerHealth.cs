using UnityEngine;
using Geneforge.Core.Stats;

namespace Geneforge.Gameplay.Characters.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Revive Settings")]
        [SerializeField] Transform respawnPoint;
        [SerializeField] float invulnerableDuration = 2f;
        [SerializeField, Range(0f, 1f)]
        float postReviveHPPercent = 1f;

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

            if (!died)
            {
                animator?.SetTrigger("Damaged");
            }
        }

        void HandleDeath()
        {
            if (runStats == null) return;
            runStats.Lives--;

            if (runStats.Lives > 0)
            {
                animator?.SetTrigger("Death");
                RevivePlayer();
            }
            else
            {

                animator?.SetTrigger("FinalDeath");
                GameOver();
            }
        }

        void RevivePlayer()
        {
            if (runStats == null) return;

            float targetHP = runStats.MaxHP * Mathf.Clamp01(postReviveHPPercent);

            runStats.Heal(targetHP);

            if (respawnPoint != null)
                transform.position = respawnPoint.position;

            isInvulnerable = true;
            invulnEndTime = Time.time + invulnerableDuration;
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
