using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Geneforge.Core.Stats; // RunStats, MetaStats

namespace Geneforge.UI
{
    public class StatsUI : MonoBehaviour
    {
        [Header("References (auto-find if empty)")]
        [SerializeField] private RunStats runStats;
        [SerializeField] private MetaStats metaStats;

        [Header("Lives Icons")]
        [SerializeField] private RectTransform livesContainer;   // leave null in scenes without lives
        [SerializeField] private Sprite lifeFullSprite;
        [SerializeField] private Sprite lifeEmptySprite;
        readonly List<Image> lifeIcons = new List<Image>();

        [Header("HP Display")]
        [SerializeField] private Image hpBarFill;                // leave null if you don’t want an HP bar
        [SerializeField] private TMP_Text hpText;                // leave null if you don’t want HP text

        [Header("Other Stats (text)")]
        [SerializeField] private TMP_Text currencyText;          // leave null if unused
        [SerializeField] private TMP_Text dnaSplicesText;        // leave null if unused
        [SerializeField] private TMP_Text rollsText;             // leave null if unused
        [SerializeField] private TMP_Text essenceText;           // leave null if unused
        [SerializeField] private TMP_Text totalDnaSplicesText;   // leave null if unused

        void Awake()
        {
            if (runStats == null)
                runStats = FindFirstObjectByType<RunStats>();
            if (metaStats == null)
                metaStats = MetaStats.I;
        }

        void Start()
        {
            // Only set up life icons if container and sprites are assigned
            if (runStats != null
                && livesContainer != null
                && lifeFullSprite != null
                && lifeEmptySprite != null)
            {
                // Use the larger of base starting lives or current lives,
                // in case meta progression increased lives.
                int maxLives = Mathf.Max(runStats.BaseStartingLives, runStats.Lives);

                for (int i = 0; i < maxLives; i++)
                {
                    var go = new GameObject("LifeIcon", typeof(Image));
                    go.transform.SetParent(livesContainer, false);
                    var img = go.GetComponent<Image>();
                    img.sprite = lifeFullSprite;
                    lifeIcons.Add(img);
                }
            }
        }

        void Update()
        {
            if (runStats != null)
            {
                // Lives
                if (lifeIcons.Count > 0)
                {
                    int lives = runStats.Lives;
                    for (int i = 0; i < lifeIcons.Count; i++)
                    {
                        lifeIcons[i].sprite = (i < lives)
                            ? lifeFullSprite
                            : lifeEmptySprite;
                    }
                }

                // HP text
                if (hpText != null)
                    hpText.text = $"{runStats.CurrentHP:0}/{runStats.MaxHP:0}";

                // HP bar
                if (hpBarFill != null && runStats.MaxHP > 0f)
                    hpBarFill.fillAmount = runStats.CurrentHP / runStats.MaxHP;

                // Currency
                if (currencyText != null)
                    currencyText.text = $"Gold: {runStats.Currency}";

                // DNA Splices
                if (dnaSplicesText != null)
                    dnaSplicesText.text = $"Splices: {runStats.DnaSplices}";

                // Rolls
                if (rollsText != null)
                    rollsText.text = $"Rolls: {runStats.Rolls}";
            }

            if (metaStats != null)
            {
                if (essenceText != null)
                    essenceText.text = $"Essence: {metaStats.Essence}";

                if (totalDnaSplicesText != null)
                    totalDnaSplicesText.text = $"Banked DNA: {metaStats.TotalDnaSplices}";
            }
        }
    }
}
