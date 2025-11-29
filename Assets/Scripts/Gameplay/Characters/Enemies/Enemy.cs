using UnityEngine;
using System;
using System.Collections;
using Geneforge.Core.UI;   // DamageText lives here now

namespace Geneforge.Gameplay.Characters.Enemies
{
    public class Enemy : MonoBehaviour
    {
        [Header("Stats")]
        public float maxHealth = 5f;
        private float currentHealth;

        [Header("Animation")]
        public Animator animator;                
        public string damagedTrigger = "Damaged";
        public string deathTrigger = "Death";
        [Tooltip("Length of the death animation clip in seconds")]
        public float deathAnimDuration = 1f;

        // Expose health
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        // Events
        public event Action OnFirstHit;
        public event Action<float> OnDamaged;

        private bool hasBeenHit = false;
        private bool isDead = false;

        [Header("Damage UI")]
        public GameObject damageTextPrefab;
        public Vector3 damageTextOffset = new Vector3(0, 2.5f, 0);

        // =====================================================
        // HIT REACTION — LIMITADO (MÁXIMO 3 NA VIDA INTEIRA)
        // =====================================================
        [Header("Hit Reaction (MAX 3 total)")]
        [Tooltip("Percentagens de vida (0..1) onde o inimigo reage com Hit.")]
        public float[] hitHealthThresholds = { 0.7f, 0.4f, 0.15f };

        int _hitReactionIndex = 0;
        // =====================================================

        // Coroutines
        private Coroutine _despawnCo;
        private Coroutine _knockbackCo;

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

            // ---------------- DAMAGE TEXT ----------------
            if (damageTextPrefab != null)
            {
                Vector3 spawnPos = transform.position + damageTextOffset;
                var dtObj = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);
                var dt = dtObj.GetComponent<DamageText>();
                if (dt != null) dt.Initialize(dmg, wasCrit);
            }
            // ---------------------------------------------

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

            // 1) parar AI/movimento imediatamente
            var animal = GetComponent<Animal.Animal>();
            if (animal != null) animal.enabled = false;

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
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector3.zero;
#else
                    rb.velocity = Vector3.zero;
#endif
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
