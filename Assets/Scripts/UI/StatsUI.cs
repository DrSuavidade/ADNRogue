using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Geneforge.Core.Stats;
using Geneforge.Gameplay.Progression;

namespace Geneforge.UI
{
    public class StatsUI : MonoBehaviour
    {
        [Header("References (auto-find if empty)")]
        [SerializeField] private RunStats runStats;
        [SerializeField] private MetaStats metaStats;

        [Header("Lives Icons")]
        [SerializeField] private RectTransform livesContainer;
        [SerializeField] private Sprite lifeFullSprite;
        [SerializeField] private Sprite lifeEmptySprite;
        readonly List<Image> lifeIcons = new List<Image>();

        [Header("HP Display")]
        [SerializeField] private Image hpBarFill;
        [SerializeField] private TMP_Text hpText;

        [Header("Other Stats (text)")]
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private TMP_Text dnaSplicesText;
        [SerializeField] private TMP_Text rollsText;
        [SerializeField] private TMP_Text essenceText;
        [SerializeField] private TMP_Text totalDnaSplicesText;

        bool _subscribed;

        void Awake()
        {
            if (runStats == null)
                runStats = RunSession.Instance != null ? RunSession.Instance.Run : FindAnyObjectByType<RunStats>();
            if (metaStats == null) metaStats = FindAnyObjectByType<MetaStats>();

            if (runStats == null)
                Debug.LogWarning("StatsUI: RunStats not found in scene.", this);
        }

        void Start()
        {
            int required = 0;

            if (runStats != null) required = Mathf.Max(required, runStats.BaseStartingLives);
            if (metaStats != null) required = Mathf.Max(required, metaStats.StartingLives);

            EnsureLifeIcons(required);
        }


        void OnEnable()
        {
            Subscribe();
            RefreshAllFromCurrentValues();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void Subscribe()
        {
            if (_subscribed) return;

            if (runStats != null)
            {
                runStats.OnLivesChanged += HandleLivesChanged;
                runStats.OnHealthChanged += HandleHealthChanged;
                runStats.OnCurrencyChanged += HandleCurrencyChanged;
                runStats.OnDnaSplicesChanged += HandleDnaSplicesChanged;
                runStats.OnRollsChanged += HandleRollsChanged;
            }

            if (metaStats != null)
            {
                metaStats.OnEssenceChanged += HandleEssenceChanged;
                metaStats.OnTotalDnaSplicesChanged += HandleTotalDnaChanged;
            }

            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;

            if (runStats != null)
            {
                runStats.OnLivesChanged -= HandleLivesChanged;
                runStats.OnHealthChanged -= HandleHealthChanged;
                runStats.OnCurrencyChanged -= HandleCurrencyChanged;
                runStats.OnDnaSplicesChanged -= HandleDnaSplicesChanged;
                runStats.OnRollsChanged -= HandleRollsChanged;
            }

            if (metaStats != null)
            {
                metaStats.OnEssenceChanged -= HandleEssenceChanged;
                metaStats.OnTotalDnaSplicesChanged -= HandleTotalDnaChanged;
            }

            _subscribed = false;
        }


        // --- Initial sync ----------------------------------------------------

        void RefreshAllFromCurrentValues()
        {
            if (runStats != null)
            {
                HandleLivesChanged(runStats.Lives);
                HandleHealthChanged(runStats.CurrentHP, runStats.MaxHP);
                HandleCurrencyChanged(runStats.Currency);
                HandleDnaSplicesChanged(runStats.DnaSplices);
                HandleRollsChanged(runStats.Rolls);
            }

            if (metaStats != null)
            {
                HandleEssenceChanged(metaStats.Essence);
                HandleTotalDnaChanged(metaStats.TotalDnaSplices);
            }
        }


        // --- Lives -----------------------------------------------------------

        void EnsureLifeIcons(int required)
        {
            if (livesContainer == null || lifeFullSprite == null || lifeEmptySprite == null)
                return;

            while (lifeIcons.Count < required)
            {
                int index = lifeIcons.Count;
                var go = new GameObject($"LifeIcon_{index}", typeof(Image));
                go.transform.SetParent(livesContainer, false);
                var img = go.GetComponent<Image>();
                img.sprite = lifeFullSprite;
                lifeIcons.Add(img);
            }
        }

        void UpdateLivesUI(int lives)
        {
            if (lifeIcons.Count == 0 || lifeFullSprite == null || lifeEmptySprite == null)
                return;

            lives = Mathf.Max(0, lives);
            for (int i = 0; i < lifeIcons.Count; i++)
            {
                var img = lifeIcons[i];
                if (img == null) continue;
                img.sprite = (i < lives) ? lifeFullSprite : lifeEmptySprite;
            }
        }

        void HandleLivesChanged(int lives)
        {
            // If lives ever increases beyond starting value (meta upgrades), expand icons.
            EnsureLifeIcons(Mathf.Max(lifeIcons.Count, lives));
            UpdateLivesUI(lives);
        }


        // --- HP --------------------------------------------------------------

        void HandleHealthChanged(float current, float max)
        {
            if (hpText != null)
                hpText.text = $"{current:0}/{max:0}";

            if (hpBarFill != null && max > 0f)
                hpBarFill.fillAmount = current / max;
        }


        // --- Run-level resources ---------------------------------------------

        void HandleCurrencyChanged(int amount)
        {
            if (currencyText != null)
                currencyText.text = $"Gold: {amount}";
        }

        void HandleDnaSplicesChanged(int amount)
        {
            if (dnaSplicesText != null)
                dnaSplicesText.text = $"Splices: {amount}";
        }

        void HandleRollsChanged(int amount)
        {
            if (rollsText != null)
                rollsText.text = $"Rolls: {amount}";
        }


        // --- Meta-level resources --------------------------------------------

        void HandleEssenceChanged(int amount)
        {
            if (essenceText != null)
                essenceText.text = $"Essence: {amount}";
        }

        void HandleTotalDnaChanged(int amount)
        {
            if (totalDnaSplicesText != null)
                totalDnaSplicesText.text = $"Banked DNA: {amount}";
        }
    }
}
