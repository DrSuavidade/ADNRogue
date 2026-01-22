using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Geneforge.Gameplay.Items;

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

        // Runtime state
        private RewardItemData _currentItem;
        private Action<RewardItemData> _onClickCallback;
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
        public void Setup(RewardItemData item, Action<RewardItemData> onClick)
        {
            _currentItem = item;
            _onClickCallback = onClick;

            // Set name
            if (itemNameText != null) itemNameText.text = item.ItemName;

            // Set description
            if (itemDescriptionText != null) itemDescriptionText.text = item.Description;

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
    }
}
