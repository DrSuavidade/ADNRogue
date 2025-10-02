using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StatsUI : MonoBehaviour
{
    [Header("References (auto-find if empty)")]
    public RunStats runStats;
    public MetaStats metaStats;

    [Header("Lives Icons")]
    public RectTransform livesContainer;   // leave null in scenes without lives
    public Sprite lifeFullSprite;
    public Sprite lifeEmptySprite;
    List<Image> lifeIcons = new List<Image>();

    [Header("HP Display")]
    public Image hpBarFill;                // leave null if you don’t want an HP bar
    public TMP_Text hpText;                // leave null if you don’t want HP text

    [Header("Other Stats (text)")]
    public TMP_Text currencyText;          // leave null if unused
    public TMP_Text dnaSplicesText;        // leave null if unused
    public TMP_Text rollsText;             // leave null if unused
    public TMP_Text essenceText;           // leave null if unused
    public TMP_Text totalDnaSplicesText;   // leave null if unused

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
            for (int i = 0; i < runStats.maxLives; i++)
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
                for (int i = 0; i < lifeIcons.Count; i++)
                    lifeIcons[i].sprite = (i < runStats.lives)
                        ? lifeFullSprite
                        : lifeEmptySprite;
            }

            // HP text
            if (hpText != null)
                hpText.text = $"{runStats.currentHP:0}/{runStats.maxHP:0}";

            // HP bar
            if (hpBarFill != null && runStats.maxHP > 0f)
                hpBarFill.fillAmount = runStats.currentHP / runStats.maxHP;

            // Currency
            if (currencyText != null)
                currencyText.text = $"Gold: {runStats.currency}";

            // DNA Splices
            if (dnaSplicesText != null)
                dnaSplicesText.text = $"Splices: {runStats.dnaSplices}";

            // Rolls
            if (rollsText != null)
                rollsText.text = $"Rolls: {runStats.rolls}";
        }

        if (metaStats != null)
        {
            if (essenceText != null)
                essenceText.text = $"Essence: {metaStats.essence}";

            if (totalDnaSplicesText != null)
                totalDnaSplicesText.text = $"Banked DNA: {metaStats.totalDnaSplices}";
        }
    }
}
