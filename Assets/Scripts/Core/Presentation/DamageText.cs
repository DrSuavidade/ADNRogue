using UnityEngine;
using System.Collections;
using TMPro;

namespace Geneforge.Core.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class DamageText : MonoBehaviour
    {
        [Tooltip("Assign the TMP_Text in the prefab")]
        public TMP_Text valueText;

        [Tooltip("Normal color for non‐critical damage")]
        public Color normalColor = Color.white;
        [Tooltip("Color for critical hits")]
        public Color critColor = Color.red;

        [Tooltip("How long before fading out and destroy")]
        public float fadeDuration = 1f;

        [Tooltip("How far it rises over fadeDuration")]
        public Vector3 riseDistance = new Vector3(0, 1f, 0);

        [Header("Combine settings")]
        [Tooltip("If another DamageText is created within this time window, combine with it instead of creating a new one.")]
        public float combineWindowSeconds = 0.06f; // ~60 ms
        [Tooltip("Only combine if the other DamageText is within this distance in world space.")]
        public float combineMaxDistance = 0.35f;

        CanvasGroup canvasGroup;
        Vector3 startPos;
        Camera cam;

        // combine state
        static readonly System.Collections.Generic.List<DamageText> s_active = new System.Collections.Generic.List<DamageText>();
        float createdAt;
        float totalDamage;
        bool anyCrit;
        bool animRunning;

        void OnEnable() { s_active.Add(this); }
        void OnDisable() { s_active.Remove(this); }

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            cam = Camera.main;
        }

        /// <summary>
        /// Call right after Instantiate.
        /// </summary>
        public void Initialize(float damage, bool wasCrit = false)
        {
            createdAt = Time.time;

            // Try to find an existing nearby DamageText to combine into
            DamageText combineTarget = FindCombineTarget();
            if (combineTarget != null)
            {
                combineTarget.Accumulate(damage, wasCrit);
                Destroy(gameObject); // we don't need this new one
                return;
            }

            // Otherwise, this instance becomes the primary one
            startPos = transform.position;
            totalDamage = Mathf.Max(0f, damage);
            anyCrit = wasCrit;

            valueText.text = Mathf.CeilToInt(totalDamage).ToString();
            valueText.color = anyCrit ? critColor : normalColor;
            canvasGroup.alpha = 1f;

            if (!animRunning)
                StartCoroutine(Animate());
        }

        /// <summary>Add more damage to an existing text (does not reset the timer).</summary>
        public void Accumulate(float amount, bool wasCrit)
        {
            totalDamage += Mathf.Max(0f, amount);
            if (wasCrit) anyCrit = true;

            valueText.text = Mathf.CeilToInt(totalDamage).ToString();
            valueText.color = anyCrit ? critColor : normalColor;
        }

        DamageText FindCombineTarget()
        {
            // Prefer the most recently created nearby text within the time & distance window
            DamageText best = null;
            float bestDt = float.PositiveInfinity;
            float now = Time.time;
            float maxDistSqr = combineMaxDistance * combineMaxDistance;

            for (int i = 0; i < s_active.Count; i++)
            {
                var dt = s_active[i];
                if (dt == this || !dt.animRunning) continue;

                float dtTime = now - dt.createdAt;
                if (dtTime > combineWindowSeconds) continue;

                float distSqr = (dt.transform.position - transform.position).sqrMagnitude;
                if (distSqr > maxDistSqr) continue;

                if (dtTime < bestDt) { bestDt = dtTime; best = dt; }
            }
            return best;
        }

        IEnumerator Animate()
        {
            animRunning = true;
            float t = 0f;
            while (t < fadeDuration)
            {
                // rise
                transform.position = startPos + riseDistance * (t / fadeDuration);
                // fade
                canvasGroup.alpha = 1f - (t / fadeDuration);
                // face camera
                if (cam != null)
                    transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
                t += Time.deltaTime;
                yield return null;
            }
            animRunning = false;
            Destroy(gameObject);
        }
    }
}
