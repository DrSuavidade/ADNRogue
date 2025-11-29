using UnityEngine;
using System.Collections;
using TMPro;

namespace Geneforge.Core.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class DamageText : MonoBehaviour
    {
        [Tooltip("Assign the TMP_Text in the prefab")]
        [SerializeField] private TMP_Text valueText;

        [Tooltip("Normal color for non‐critical damage")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("Color for critical hits")]
        [SerializeField] private Color critColor = Color.red;

        [Tooltip("How long before fading out and destroy")]
        [SerializeField] private float fadeDuration = 1f;

        [Tooltip("How far it rises over fadeDuration")]
        [SerializeField] private Vector3 riseDistance = new Vector3(0, 1f, 0);

        [Header("Combine settings")]
        [Tooltip("If another DamageText is created within this time window, combine with it instead of creating a new one.")]
        [SerializeField] private float combineWindowSeconds = 0.06f; // ~60 ms
        [Tooltip("Only combine if the other DamageText is within this distance in world space.")]
        [SerializeField] private float combineMaxDistance = 0.35f;

        static readonly System.Collections.Generic.List<DamageText> s_active =
            new System.Collections.Generic.List<DamageText>();

        CanvasGroup canvasGroup;
        Vector3 startPos;
        Camera cam;

        float createdAt;
        float totalDamage;
        bool anyCrit;
        bool animRunning;

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (Camera.main != null)
                cam = Camera.main;

            if (valueText == null)
            {
                valueText = GetComponentInChildren<TMP_Text>();
                if (valueText == null)
                    Debug.LogError($"DamageText on {name} has no TMP_Text assigned.");
            }
        }

        void OnEnable()
        {
            if (!s_active.Contains(this))
                s_active.Add(this);
        }

        void OnDisable()
        {
            s_active.Remove(this);
            animRunning = false;
        }

        /// <summary>Call right after Instantiate.</summary>
        public void Initialize(float damage, bool wasCrit = false)
        {
            createdAt = Time.time;

            var combineTarget = FindCombineTarget();
            if (combineTarget != null)
            {
                combineTarget.Accumulate(damage, wasCrit);
                Destroy(gameObject);
                return;
            }

            startPos = transform.position;
            totalDamage = Mathf.Max(0f, damage);
            anyCrit = wasCrit;

            if (valueText != null)
            {
                valueText.text = Mathf.CeilToInt(totalDamage).ToString();
                valueText.color = anyCrit ? critColor : normalColor;
            }

            canvasGroup.alpha = 1f;

            if (!animRunning)
                StartCoroutine(Animate());
        }

        public void Accumulate(float amount, bool wasCrit)
        {
            totalDamage += Mathf.Max(0f, amount);
            if (wasCrit) anyCrit = true;

            if (valueText != null)
            {
                valueText.text = Mathf.CeilToInt(totalDamage).ToString();
                valueText.color = anyCrit ? critColor : normalColor;
            }
        }

        DamageText FindCombineTarget()
        {
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
            if (fadeDuration <= 0f)
            {
                Destroy(gameObject);
                yield break;
            }
            
            animRunning = true;
            float t = 0f;

            while (t < fadeDuration)
            {
                float normalized = t / fadeDuration;

                transform.position = startPos + riseDistance * normalized;

                canvasGroup.alpha = 1f - normalized;

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
