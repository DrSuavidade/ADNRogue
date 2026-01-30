using UnityEngine;
using Geneforge.Gameplay.Characters.Player;

namespace Geneforge.Gameplay.Characters.Enemies.Abilities
{
    /// <summary>
    /// Handles melee damage dealing, ideally triggered by Animation Events.
    /// Usage: Add to enemy. If using Animation Events, call 'AnimEvent_MeleeHit'.
    /// If no events are available, this component can handle a simple timer-based hit.
    /// </summary>
    public class MeleeAttackAbility : MonoBehaviour
    {
        [Header("Damage Config")]
        [SerializeField] private float damage = 10f;
        [SerializeField] private float reach = 1.5f;
        [SerializeField] private float hitAngle = 45f; // Field of view for hit
        [SerializeField] private LayerMask hitLayers;

        [Header("Timing (Fallback)")]
        [Tooltip("If getting called by script (manual) instead of AnimEvent, delay damage by this amount.")]
        [SerializeField] private float hitDelay = 0.4f;

        private Transform _target;
        private Coroutine _attackRoutine;

        public void Configure(float dmg, float rng)
        {
            damage = dmg;
            reach = rng;
        }

        public void SetTarget(Transform t)
        {
            _target = t;
        }

        /// <summary>
        /// Called by Brain to start an attack.
        /// </summary>
        public void BeginAttack()
        {
            // If using Animation Events, this method just sets state or does nothing.
            // If relying on auto-timing (because we lack events on the clips), start the routine.
            // For now, we assume mixed usage, so we'll start a fallback delay if no event triggers.
            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            _attackRoutine = StartCoroutine(AttackRoutine());
        }

        private System.Collections.IEnumerator AttackRoutine()
        {
            yield return new WaitForSeconds(hitDelay);
            PerformHitCheck();
            _attackRoutine = null;
        }

        /// <summary>
        /// Call this FROM THE ANIMATION EVENT "AnimEvent_MeleeHit"
        /// </summary>
        public void AnimEvent_MeleeHit()
        {
            // If we have a routine running (fallback), cancel it so we don't double hit
            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            
            PerformHitCheck();
        }

        private void PerformHitCheck()
        {
            if (_target == null) return;

            // 1. Distance Check
            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist > reach) return;

            // 2. Body-facing Check (optional, ensures we don't hit behind us)
            Vector3 toTarget = (_target.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, toTarget) > hitAngle) return;

            // 3. Damage
            var hp = _target.GetComponent<PlayerHealth>();
            if (hp != null)
            {
                hp.ApplyDamage(damage);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, reach);
            
            Vector3 left = Quaternion.Euler(0, -hitAngle, 0) * transform.forward;
            Vector3 right = Quaternion.Euler(0, hitAngle, 0) * transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + left * reach);
            Gizmos.DrawLine(transform.position, transform.position + right * reach);
        }
    }
}
