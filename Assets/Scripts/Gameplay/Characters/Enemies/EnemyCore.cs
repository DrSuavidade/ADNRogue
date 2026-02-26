using UnityEngine;
using System;
using System.Collections;
using Geneforge.Core.UI;
using Geneforge.Core.Pooling;
using Geneforge.Gameplay.Characters.Enemies.AI;
using Geneforge.Gameplay.Characters.UI;
using Geneforge.Gameplay.Map;

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

        [Header("Professional Stagger System (Poise)")]
        [Tooltip("Amount of damage needed to stagger the enemy. Low for small enemies, high for big ones.")]
        [SerializeField] private float maxPoise = 20f;
        [Tooltip("How fast poise recovers per second when not taking damage.")]
        [SerializeField] private float poiseRecoveryRate = 5f;
        private float currentPoise;

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
        public float HealthBarHeightOverride => healthBarHeightOverride;


        // Events
        public event Action OnFirstHit;
        public event Action<float> OnDamaged;
        public event Action OnStaggered;   // triggered only when poise is broken
        public event Action<Vector3, float> OnKnockback; // NEW: event for brains
        public event Action OnDied;        // NEW: death event for brains / systems

        private bool hasBeenHit = false;
        private bool isDead = false;

        public bool HasBeenHit => hasBeenHit; // NEW: in case brains/UI need this
        public bool IsDead => isDead;         // NEW: convenient read for brains

        float _lastDamageTime;

        // Coroutines
        private Coroutine _despawnCo;
        private Coroutine _knockbackCo;
        HealthBar _healthBarInstance;

        // Caching
        private Collider _collider;
        private CharacterController _characterController;
        private EnemyBrainBase _brain;
        private EnemyDeathNotifier _deathNotifier;
        private Rigidbody[] _rigidbodies;

        void Awake()
        {
            currentHealth = maxHealth;
            currentPoise = maxPoise;

            // Cache components
            _collider = GetComponent<Collider>();
            _characterController = GetComponent<CharacterController>();
            _brain = GetComponent<EnemyBrainBase>();
            _deathNotifier = GetComponent<EnemyDeathNotifier>();
            _rigidbodies = GetComponentsInChildren<Rigidbody>();

            if (animator == null) animator = GetComponentInChildren<Animator>();

            SpawnHealthBarIfNeeded();
        }

        void OnEnable()
        {
            // If enemies are reused or enabled/disabled, ensure health and bar are valid
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            currentPoise = maxPoise;
            isDead = false;
            hasBeenHit = false;

            if (_collider != null) _collider.enabled = true;
            if (_characterController != null) _characterController.enabled = true;

            SpawnHealthBarIfNeeded();
        }

        void Update()
        {
            if (isDead) return;

            // Recover poise over time
            if (currentPoise < maxPoise && Time.time > _lastDamageTime + 1.5f)
            {
                currentPoise = Mathf.MoveTowards(currentPoise, maxPoise, poiseRecoveryRate * Time.deltaTime);
            }
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
        public bool IsInvulnerable { get; set; } = false;

        public void TakeDamage(float dmg, bool wasCrit = false)
        {
            if (dmg <= 0f || isDead || IsInvulnerable) return;

            if (!hasBeenHit)
            {
                hasBeenHit = true;
                // Note: No animation on first hit anymore, only pure logic/events
                OnFirstHit?.Invoke();
            }

            currentHealth = Mathf.Max(0f, currentHealth - dmg);
            _lastDamageTime = Time.time;

            SpawnDamageText(dmg, wasCrit);
            OnDamaged?.Invoke(dmg);

            // =====================================================
            // PROFESSIONAL POISE SYSTEM 
            // =====================================================
            if (currentHealth > 0f)
            {
                bool canStagger = _brain == null || !_brain.HasHyperArmor;

                if (canStagger)
                {
                    currentPoise -= dmg;

                    if (currentPoise <= 0f)
                    {
                        // THE BIG BREAK: This is where we reward the player
                        if (animator != null)
                        {
                            animator.SetTrigger(damagedTrigger);
                        }

                        currentPoise = maxPoise; // Reset for next break
                        OnStaggered?.Invoke(); 
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

            // Notify brain
            if (_brain != null)
            {
                _brain.OnOwnerDied();
            }

            if (_collider != null) _collider.enabled = false;
            if (_characterController != null) _characterController.enabled = false;

            // Stop rigidbodies
            if (_rigidbodies != null)
            {
                foreach (var rb in _rigidbodies)
                {
                    if (rb == null) continue;
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                }
            }

            // Death animation
            if (animator != null)
            {
                animator.ResetTrigger(damagedTrigger);
                animator.SetFloat("Speed", 0f);
                animator.SetTrigger(deathTrigger);
            }

            // Notify listeners
            OnDied?.Invoke();

            if (_deathNotifier != null)
            {
                _deathNotifier.ReportDeath();
            }

            // Despawn
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
            
            _despawnCo = null;

            // TODO: Use pooling for enemies if desired for even better performance
            Destroy(gameObject);
        }

        public void Heal(float amount, bool showText = true)
        {
            if (amount <= 0f || isDead) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

            if (showText && damageTextPrefab != null)
            {
                // optionally spawn green “+X” text
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

            _healthBarInstance.Initialize(this);
        }


        public void ApplyKnockback(Vector3 direction, float force, float duration = 0.12f)
        {
            if (isDead || force <= 0f) return;

            if (_brain != null && _brain.HasHyperArmor) return;

            OnKnockback?.Invoke(direction, duration);

            float totalDisplacement = 0.05f * force;
            Vector3 shiftVector = direction.normalized * totalDisplacement;

            if (_knockbackCo != null) StopCoroutine(_knockbackCo);
            _knockbackCo = StartCoroutine(KnockbackRoutine(shiftVector, duration));
        }

        IEnumerator KnockbackRoutine(Vector3 totalShift, float duration)
        {
            float elapsed = 0f;
            float lastCurve = 0f;
            
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float curve = 1f - Mathf.Pow(1f - t, 3);
                float deltaCurve = curve - lastCurve;
                lastCurve = curve;

                Vector3 deltaMove = totalShift * deltaCurve;

                if (_characterController != null && _characterController.enabled)
                {
                    _characterController.Move(deltaMove);
                }
                else
                {
                    transform.position += deltaMove;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
            _knockbackCo = null;
        }
    }
}

