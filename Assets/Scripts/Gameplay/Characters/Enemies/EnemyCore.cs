using UnityEngine;
using System;
using System.Collections;
using Geneforge.Core.UI;
using Geneforge.Core.Pooling;
using Geneforge.Gameplay.Characters.Enemies.AI;
using Geneforge.Gameplay.Characters.UI;

namespace Geneforge.Gameplay.Characters.Enemies
{
    public class EnemyCore : MonoBehaviour, IEnemy
    {
        [Header("Stats")]
        [SerializeField] private float maxHealth = 5f;
        private float currentHealth;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string damagedTrigger = "Damaged";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private float deathAnimDuration = 1f;

        [Header("Feedback")]
        [SerializeField] private GameObject damageTextPrefab;
        [SerializeField] private Vector3 damageTextOffset = new Vector3(0, 2.5f, 0);

        [Header("Hit thresholds")]
        [SerializeField] private float[] hitHealthThresholds = { 0.7f, 0.4f, 0.15f };

        [Header("UI")]
        [Tooltip("Pooled world-space health bar prefab (must have HealthBar + PoolIdentifier).")]
        [SerializeField] private GameObject healthBarPrefab;

        [Tooltip("Optional explicit height (in world units) above the enemy pivot for the health bar. " +
         "If <= 0, a height is auto-estimated from colliders/CharacterController.")]
        [SerializeField] private float healthBarHeightOverride = -1f;

        // Expose fields
        public float CurrentHealth => currentHealth;
        public float MaxHealth { get => maxHealth; set => maxHealth = value; }
        public Animator Animator => animator;
        public string DamagedTrigger => damagedTrigger;
        public string DeathTrigger => deathTrigger;
        public float DeathAnimDuration => deathAnimDuration;

        public GameObject DamageTextPrefab => damageTextPrefab;
        public Vector3 DamageTextOffset => damageTextOffset;
        public float[] HitHealthThresholds => hitHealthThresholds;
        public float HealthBarHeightOverride => healthBarHeightOverride;


        // Events
        public event Action OnFirstHit;
        public event Action<float> OnDamaged;
        public event Action OnDied;        // NEW: death event for brains / systems

        private bool hasBeenHit = false;
        private bool isDead = false;

        public bool HasBeenHit => hasBeenHit; // NEW: in case brains/UI need this
        public bool IsDead => isDead;         // NEW: convenient read for brains

        int _hitReactionIndex = 0;

        // Coroutines
        private Coroutine _despawnCo;
        private Coroutine _knockbackCo;
        HealthBar _healthBarInstance;

        void Awake()
        {
            currentHealth = maxHealth;
            SpawnHealthBarIfNeeded();
        }

        void OnEnable()
        {
            // If enemies are reused or enabled/disabled, ensure health and bar are valid
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            isDead = false;
            hasBeenHit = false;
            _hitReactionIndex = 0;

            SpawnHealthBarIfNeeded();
        }

        void OnDisable()
        {
            // Clean up our health bar (return to pool if applicable)
            if (_healthBarInstance != null)
            {
                var hbGO = _healthBarInstance.gameObject;
                _healthBarInstance = null;

                var poolId = hbGO.GetComponent<PoolIdentifier>();
                if (poolId != null && PoolManager.Instance != null)
                {
                    PoolManager.Instance.Reclaim(hbGO);
                }
                else
                {
                    Destroy(hbGO);
                }
            }
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

            SpawnDamageText(dmg, wasCrit);

            OnDamaged?.Invoke(dmg);

            // =====================================================
            // HIT REACTION — DISPARA SÓ 3 VEZES NO TOTAL
            // =====================================================
            if (currentHealth > 0f && animator != null)
            {
                if (_hitReactionIndex < hitHealthThresholds.Length)
                {
                    float thresholdHP = maxHealth * hitHealthThresholds[_hitReactionIndex];

                    if (currentHealth <= thresholdHP)
                    {
                        animator.SetTrigger(damagedTrigger);
                        _hitReactionIndex++; // nunca mais repete este marco
                    }
                }
            }
            // =====================================================

            if (currentHealth <= 0f)
                Die();
        }

        void Die()
        {
            if (isDead) return;
            isDead = true;

            // NEW: disable brain in a clean, centralized way
            var brain = GetComponent<EnemyBrainBase>();
            if (brain != null)
            {
                // Let the brain clean itself up (states, timers, etc.)
                brain.OnOwnerDied();
            }

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Zerar velocidades apenas se NÃO forem cinemáticos
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
            {
                if (rb == null) continue;

                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                rb.isKinematic = true;
            }

            // 2) animação de morte
            if (animator != null)
            {
                animator.ResetTrigger(damagedTrigger);
                animator.SetFloat("Speed", 0f);
                animator.SetTrigger(deathTrigger);
            }

            // Notify listeners (brains, loot systems, etc.)
            OnDied?.Invoke();   // NEW

            GetComponent<Map.EnemyDeathNotifier>()?.ReportDeath();

            // 3) despawn em tempo REAL
            if (_despawnCo == null)
                _despawnCo = StartCoroutine(DespawnAfterRealtime(5f));
        }

        IEnumerator DespawnAfterRealtime(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Destroy(gameObject);
        }

        public void Heal(float amount, bool showText = true)
        {
            if (amount <= 0f || isDead) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

            if (showText && damageTextPrefab != null)
            {
                // optionally spawn green “+X” text; re-use pooled DamageText if needed
            }
        }

        void SpawnDamageText(float dmg, bool wasCrit)
        {
            if (damageTextPrefab == null) return;

            Vector3 spawnPos = transform.position + damageTextOffset;
            GameObject dtObj = PoolManager.Instance != null
                ? PoolManager.Instance.Spawn(damageTextPrefab, spawnPos, Quaternion.identity)
                : Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);

            var dt = dtObj.GetComponent<DamageText>();
            if (dt != null) dt.Initialize(dmg, wasCrit);
        }

        void SpawnHealthBarIfNeeded()
        {
            if (healthBarPrefab == null) return;
            if (_healthBarInstance != null) return;

            GameObject barGO;

            if (PoolManager.Instance != null)
            {
                barGO = PoolManager.Instance.Spawn(
                    healthBarPrefab,
                    transform.position,
                    Quaternion.identity
                );
            }
            else
            {
                barGO = Instantiate(
                    healthBarPrefab,
                    transform.position,
                    Quaternion.identity
                );
            }

            _healthBarInstance = barGO.GetComponent<HealthBar>();
            if (_healthBarInstance == null)
            {
                Debug.LogWarning($"EnemyCore on {name} spawned a healthBarPrefab with no HealthBar component.", barGO);
                return;
            }

            // Bind this bar to this enemy
            _healthBarInstance.Initialize(this);
        }


        // Smooth knockback slide — sem StopAllCoroutines()
        public void ApplyKnockback(Vector3 direction, float force, float duration = 0.1f)
        {
            if (isDead) return;

            float disp = 0.1f * force;
            Vector3 targetPos = transform.position + direction.normalized * disp;

            if (_knockbackCo != null) StopCoroutine(_knockbackCo);
            _knockbackCo = StartCoroutine(KnockbackRoutine(transform.position, targetPos, duration));
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
            _knockbackCo = null;
        }
    }
}
