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
        public Animator animator;                // assign your Animator
        public string damagedTrigger = "Damaged";
        public string deathTrigger = "Death";
        [Tooltip("Length of the death animation clip in seconds")]
        public float deathAnimDuration = 1f;     // já não usamos para Destroy, mas podes manter

        // Expose health
        public float CurrentHealth => currentHealth;
        public float MaxHealth   => maxHealth;

        // Fired the first time the enemy is damaged
        public event Action OnFirstHit;
        public event Action<float> OnDamaged;

        private bool hasBeenHit = false;
        private bool isDead     = false;

        [Header("Damage UI")]
        public GameObject damageTextPrefab;  // assign your DamageText prefab
        public Vector3 damageTextOffset = new Vector3(0, 2.5f, 0);

        // --- NEW: refs de coroutines para não as matar acidentalmente ---
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

            if (damageTextPrefab != null)
            {
                Vector3 spawnPos = transform.position + damageTextOffset;
                var dtObj = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);
                var dt = dtObj.GetComponent<DamageText>();
                if (dt != null) dt.Initialize(dmg, wasCrit);
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

            // 1) parar AI/movimento imediatamente
            var animal = GetComponent<Geneforge.Gameplay.Characters.Enemies.Animal.Animal>();
            if (animal != null) animal.enabled = false;

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Zerar velocidades apenas se NÃO forem cinemáticos e só depois tornar kinematic
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

            // 3) despawn em tempo REAL (não para com pause)
            if (_despawnCo == null)
                _despawnCo = StartCoroutine(DespawnAfterRealtime(5f));
        }

        // Usa tempo não escalado → funciona mesmo com Time.timeScale = 0
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

        // Smooth knockback slide — agora sem StopAllCoroutines()
        public void ApplyKnockback(Vector3 direction, float force, float duration = 0.1f)
        {
            if (isDead) return; // não empurrar cadáver

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
