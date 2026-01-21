using System;
using System.Collections.Generic;
using UnityEngine;
using Geneforge.Gameplay.Items;

namespace Geneforge.UI
{
    /// <summary>
    /// UI Manager for the reward chest selection panel.
    /// Shows 3 items with animated sprite previews.
    /// Implements IRewardChestUIProvider and registers with RewardChestUIService.
    /// </summary>
    public class RewardChestUI : MonoBehaviour, IRewardChestUIProvider
    {
        [Header("UI References")]
        [Tooltip("The root panel to show/hide.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("The item slot components (should have exactly 3).")]
        [SerializeField] private List<RewardChestSlot> itemSlots = new List<RewardChestSlot>();

        [Header("Settings")]
        [Tooltip("Pause the game while the panel is open.")]
        [SerializeField] private bool pauseGameOnOpen = true;

        [Tooltip("Hide cursor override - set false if you manage cursor elsewhere.")]
        [SerializeField] private bool manageCursor = true;

        // Runtime state
        private List<RewardItemData> _currentItems;
        private GameObject _playerRef;
        private Action<RewardItemData, GameObject> _onSelectionCallback;
        private bool _isOpen = false;
        private float _previousTimeScale;
        private CursorLockMode _previousCursorLockMode;
        private bool _previousCursorVisible;

        private void Awake()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // Register this UI with the service locator
            RewardChestUIService.Register(this);
        }

        private void OnDisable()
        {
            // Unregister from the service locator
            RewardChestUIService.Unregister(this);
        }

        /// <summary>
        /// Display the reward selection panel with the given items.
        /// </summary>
        /// <param name="items">Items to display (up to 3).</param>
        /// <param name="player">Reference to the player GameObject.</param>
        /// <param name="onSelection">Callback when an item is selected.</param>
        public void ShowRewardSelection(List<RewardItemData> items, GameObject player, Action<RewardItemData, GameObject> onSelection)
        {
            if (_isOpen) return;

            _currentItems = items;
            _playerRef = player;
            _onSelectionCallback = onSelection;
            _isOpen = true;

            // Setup slots
            for (int i = 0; i < itemSlots.Count; i++)
            {
                if (i < items.Count && items[i] != null)
                {
                    itemSlots[i].Setup(items[i], OnSlotClicked);
                    itemSlots[i].gameObject.SetActive(true);
                }
                else
                {
                    itemSlots[i].gameObject.SetActive(false);
                }
            }

            // Show panel
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            // Pause game
            if (pauseGameOnOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            // Show cursor
            if (manageCursor)
            {
                _previousCursorLockMode = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// Called when a slot is clicked.
        /// </summary>
        private void OnSlotClicked(RewardItemData item)
        {
            if (!_isOpen) return;

            ClosePanel();

            _onSelectionCallback?.Invoke(item, _playerRef);
        }

        /// <summary>
        /// Close the panel and restore game state.
        /// </summary>
        private void ClosePanel()
        {
            _isOpen = false;

            // Hide panel
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            // Stop all slot animations
            foreach (var slot in itemSlots)
            {
                slot.StopAnimation();
            }

            // Restore time
            if (pauseGameOnOpen)
            {
                Time.timeScale = _previousTimeScale;
            }

            // Restore cursor
            if (manageCursor)
            {
                Cursor.lockState = _previousCursorLockMode;
                Cursor.visible = _previousCursorVisible;
            }

            _currentItems = null;
            _playerRef = null;
            _onSelectionCallback = null;
        }

        /// <summary>
        /// Allow closing with escape key (optional skip).
        /// </summary>
        private void Update()
        {
            if (!_isOpen) return;

            // Optional: Close on Escape (skip selection)
            // Uncomment if you want to allow skipping:
            // if (Input.GetKeyDown(KeyCode.Escape))
            // {
            //     ClosePanel();
            //     _onSelectionCallback?.Invoke(null, _playerRef);
            // }
        }
    }
}
