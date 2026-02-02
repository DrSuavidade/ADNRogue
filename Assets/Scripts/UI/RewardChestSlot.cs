using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Geneforge.Gameplay.Items;
using Geneforge.Gameplay.Abilities; // For WeaponStatId and existing types
using System.Collections.Generic; // Ensure List is available
using System.Linq; // For easier icon lookup

namespace Geneforge.UI
{
    /// <summary>
    /// Individual slot in the reward selection panel.
    /// Displays an item with animated sprite cycling (video-like effect).
    /// </summary>
    public class RewardChestSlot : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Image component that displays the animated item preview.")]
        [SerializeField] private Image itemImage;

        [Tooltip("Text component for item name.")]
        [SerializeField] private TMP_Text itemNameText;

        [Tooltip("Text component for item description.")]
        [SerializeField] private TMP_Text itemDescriptionText;

        [Tooltip("Button component for selection.")]
        [SerializeField] private Button selectButton;

        [Header("Rarity Colors")]
        [SerializeField] private Color commonColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] private Color rareColor = new Color(0.2f, 0.4f, 1f);
        [SerializeField] private Color epicColor = new Color(0.6f, 0.2f, 0.8f);
        [SerializeField] private Color legendaryColor = new Color(1f, 0.6f, 0f);
        [SerializeField] private Color mythicColor = new Color(0.2f, 0.8f, 0.2f);

        [Header("Visual Feedback")]
        [Tooltip("Optional glow/border image that changes color based on rarity.")]
        [SerializeField] private Image rarityBorder;

        [Header("Stats Visualization")]
        [SerializeField] private Transform statsContainer;
        [SerializeField] private RewardChestStatRow statRowPrefab;

        // Runtime state
        private RewardItemData _currentItem;
        private Action<RewardItemData> _onClickCallback;
        private RewardStatConfig _statConfig;
        private int _currentFrameIndex;
        private float _nextFrameTime;
        private bool _isAnimating = false;

        private void Awake()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnButtonClicked);
            }
        }

        private void OnDestroy()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(OnButtonClicked);
            }
        }

        private void Update()
        {
            if (!_isAnimating || _currentItem == null || _currentItem.AnimationFrames == null || _currentItem.AnimationFrames.Count <= 1)
                return;

            // Use unscaledTime so it works while the game is paused
            if (Time.unscaledTime >= _nextFrameTime)
            {
                float frameDelay = 1f / Mathf.Max(1f, _currentItem.FramesPerSecond);
                _nextFrameTime = Time.unscaledTime + frameDelay;

                _currentFrameIndex = (_currentFrameIndex + 1) % _currentItem.AnimationFrames.Count;

                if (itemImage != null && _currentItem.AnimationFrames[_currentFrameIndex] != null)
                {
                    itemImage.sprite = _currentItem.AnimationFrames[_currentFrameIndex];
                }
            }
        }

        /// <summary>
        /// Setup the slot with an item.
        /// </summary>
        public void Setup(RewardItemData item, Action<RewardItemData> onClick, RewardStatConfig config)
        {
            _currentItem = item;
            _onClickCallback = onClick;
            _statConfig = config;

            // Set name
            if (itemNameText != null) itemNameText.text = item.ItemName;

            // Set description (Disabled in favor of Stats, but kept for fallback if desired)
            if (itemDescriptionText != null) 
            {
                itemDescriptionText.gameObject.SetActive(false); // Hide description
                // itemDescriptionText.text = item.Description; 
            }

            // Setup Stats Visualization
            SetupStats(item);

            // Set rarity color
            Color rarityColor = GetRarityColor(item.Rarity);
            if (itemNameText != null) itemNameText.color = rarityColor;
            if (rarityBorder != null) rarityBorder.color = rarityColor;

            // Setup initial frame
            _currentFrameIndex = 0;
            _isAnimating = false;

            if (itemImage != null)
            {
                if (item.AnimationFrames != null && item.AnimationFrames.Count > 0)
                {
                    itemImage.sprite = item.AnimationFrames[0];
                    itemImage.enabled = true;
                    
                    if (item.AnimationFrames.Count > 1)
                    {
                        _isAnimating = true;
                        float frameDelay = 1f / Mathf.Max(1f, item.FramesPerSecond);
                        _nextFrameTime = Time.unscaledTime + frameDelay;
                    }
                }
                else
                {
                    itemImage.sprite = item.Icon;
                    itemImage.enabled = item.Icon != null;
                }
            }
        }

        /// <summary>
        /// Stop the animation.
        /// </summary>
        public void StopAnimation()
        {
            _isAnimating = false;
        }

        private void OnButtonClicked()
        {
            _onClickCallback?.Invoke(_currentItem);
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return commonColor;
                case ItemRarity.Rare: return rareColor;
                case ItemRarity.Epic: return epicColor;
                case ItemRarity.Legendary: return legendaryColor;
                case ItemRarity.Mythic: return mythicColor;
                default: return commonColor;
            }
        }

        private void SetupStats(RewardItemData item)
        {
            // Clear existing stats
            if (statsContainer != null)
            {
                foreach (Transform child in statsContainer)
                {
                    Destroy(child.gameObject);
                }
            }
            else
            {
                return; // No container to spawn into
            }

            if (statRowPrefab == null) return;

            // 1. Run Modifiers
            // Note: RewardItemData uses Geneforge.Gameplay.Items.RewardStatModifier
            // and Geneforge.Gameplay.Items.ModifierKind
            foreach (var mod in item.StatModifiers)
            {
                 // Check if it effectively changes anything
                 if (Mathf.Abs(mod.value) < 0.0001f && mod.kind == Geneforge.Gameplay.Items.ModifierKind.Add) continue;
                 if (Mathf.Abs(mod.value - 1f) < 0.0001f && mod.kind == Geneforge.Gameplay.Items.ModifierKind.Multiply) continue;

                 SpawnStatRow(
                     GetRunStatIcon(mod.stat),
                     IsRunStatUpgrade(mod.stat, mod.value, mod.kind)
                 );
            }

            // 2. Weapon Modifiers
            // Note: RewardItemData uses Geneforge.Gameplay.Abilities.StatModifier (implied by previous context)
            // and Geneforge.Gameplay.Abilities.ModifierKind
            foreach (var mod in item.WeaponModifiers)
            {
                 // Check if it effectively changes anything
                 if (Mathf.Abs(mod.value) < 0.0001f && mod.kind == Geneforge.Gameplay.Abilities.ModifierKind.Add) continue;
                 if (Mathf.Abs(mod.value - 1f) < 0.0001f && mod.kind == Geneforge.Gameplay.Abilities.ModifierKind.Multiply) continue;

                 SpawnStatRow(
                     GetWeaponStatIcon(mod.stat),
                     IsWeaponStatUpgrade(mod.stat, mod.value, mod.kind)
                 );
            }
        }

        private void SpawnStatRow(Sprite icon, bool isUpgrade)
        {
            if (icon == null || _statConfig == null) return; // Don't show if no icon
            
            var rowObj = Instantiate(statRowPrefab, statsContainer);
            rowObj.Setup(icon, isUpgrade ? _statConfig.UpgradeArrow : _statConfig.DowngradeArrow, isUpgrade);
        }

        private Sprite GetRunStatIcon(Geneforge.Gameplay.Items.StatType stat)
        {
            if (_statConfig == null) return null;
            return _statConfig.GetRunStatIcon(stat);
        }

        private Sprite GetWeaponStatIcon(Geneforge.Gameplay.Abilities.WeaponStatId stat)
        {
             if (_statConfig == null) return null;
            return _statConfig.GetWeaponStatIcon(stat);
        }

        private bool IsRunStatUpgrade(Geneforge.Gameplay.Items.StatType stat, float value, Geneforge.Gameplay.Items.ModifierKind kind)
        {
             // For Run Stats, almost everything is "Higher = Better".
             // Logic:
             // Add: value > 0 is Upgrade.
             // Mult: value > 1 is Upgrade.
             
             bool increases = (kind == Geneforge.Gameplay.Items.ModifierKind.Add && value > 0) ||
                              (kind == Geneforge.Gameplay.Items.ModifierKind.Multiply && value > 1f);

             // Are there any Run Stats where "Lower is Better"?
             // Health, Lives, Currency, Dna, Rolls, Speed, Luck -> All Good if Increased.
             // So 'increases' == Upgrade.

             return increases;
        }

        private bool IsWeaponStatUpgrade(Geneforge.Gameplay.Abilities.WeaponStatId stat, float value, Geneforge.Gameplay.Abilities.ModifierKind kind)
        {
            bool increases = (kind == Geneforge.Gameplay.Abilities.ModifierKind.Add && value > 0) ||
                             (kind == Geneforge.Gameplay.Abilities.ModifierKind.Multiply && value > 1f);

            // Handle "Lower is Better" stats
            switch (stat)
            {
                case Geneforge.Gameplay.Abilities.WeaponStatId.FireRate:
                case Geneforge.Gameplay.Abilities.WeaponStatId.InaccuracyHalfAngle:
                    return !increases; // Decrease is Upgrade
                
                default:
                    return increases; // Increase is Upgrade
            }
        }
    }
}
