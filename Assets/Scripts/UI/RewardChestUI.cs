using System;
using System.Collections.Generic;
using UnityEngine;
using Geneforge.Gameplay.Items;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Cameras;

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

#if ENABLE_INPUT_SYSTEM
        private UnityEngine.InputSystem.PlayerInput _playerInput;
#endif
        private MonoBehaviour[] _disabledComponents;

        private void Awake()
        {
            // Register as early as possible
            RewardChestUIService.Register(this);

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[RewardChestUI] Panel Root is not assigned in the inspector!");
            }
        }

        private void OnEnable()
        {
            // Ensure registration if it was somehow lost
            if (RewardChestUIService.Provider == null)
                RewardChestUIService.Register(this);
        }

        /// <summary>
        /// Display the reward selection panel with the given items.
        /// </summary>
        /// <param name="items">Items to display (up to 3).</param>
        /// <param name="player">Reference to the player GameObject.</param>
        /// <param name="onSelection">Callback when an item is selected.</param>
        public void ShowRewardSelection(List<RewardItemData> items, GameObject player, Action<RewardItemData, GameObject> onSelection)
        {
            Debug.Log($"[RewardChestUI] ShowRewardSelection called with {items.Count} items.");
            if (_isOpen) 
            {
                Debug.Log("[RewardChestUI] Panel is already open, ignoring request.");
                return;
            }

            _currentItems = items;
            _playerRef = player;
            _onSelectionCallback = onSelection;
            _isOpen = true;

            // 1. Show panel and force hierarchy visibility
            if (panelRoot != null)
            {
                Debug.Log($"[RewardChestUI] Activating panelRoot: {panelRoot.name}");
                panelRoot.SetActive(true);

                // FORCE VISIBILITY: Ensure Canvas and scale are correct
                var canvas = panelRoot.GetComponentInParent<Canvas>();
                if (canvas != null) canvas.enabled = true;

                var rect = panelRoot.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localScale = Vector3.one; // Ensure it's not scale 0
                    // rect.anchoredPosition = Vector2.zero; // Uncomment if it might be off-screen
                }

                var group = panelRoot.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = 1f;
                    group.blocksRaycasts = true;
                    group.interactable = true;
                }
            }

            // 2. Setup slots
            for (int i = 0; i < itemSlots.Count; i++)
            {
                if (i < items.Count && items[i] != null)
                {
                    Debug.Log($"[RewardChestUI] Setting up slot {i} with item {items[i].ItemName}");
                    itemSlots[i].gameObject.SetActive(true);
                    itemSlots[i].Setup(items[i], OnSlotClicked);
                }
                else
                {
                    itemSlots[i].gameObject.SetActive(false);
                }
            }

            // Disable player input so they don't move or rotate camera
            TogglePlayerInput(player, false);

            // Pause game
            if (pauseGameOnOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                Debug.Log("[RewardChestUI] Game paused successfully.");
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

        private void TogglePlayerInput(GameObject player, bool enabled)
        {
            if (player == null) return;

            if (!enabled)
            {
                List<MonoBehaviour> toDisable = new List<MonoBehaviour>();

                // 1. Disable PlayerInput if using Input System
#if ENABLE_INPUT_SYSTEM
                _playerInput = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                if (_playerInput == null) _playerInput = player.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>();
                
                if (_playerInput != null)
                {
                    _playerInput.enabled = false;
                    Debug.Log("[RewardChestUI] PlayerInput disabled.");
                }
#endif

                // 2. Disable PlayerController component (stops movement/shooting)
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.enabled = false;
                    toDisable.Add(pc);
                    Debug.Log("[RewardChestUI] PlayerController disabled.");
                }

                // 3. Disable OrbitCamera component (stops camera rotation)
                var cam = Camera.main;
                if (cam != null)
                {
                    var orbit = cam.GetComponent<OrbitCamera>();
                    if (orbit == null) orbit = cam.GetComponentInParent<OrbitCamera>();
                    
                    if (orbit != null)
                    {
                        orbit.enabled = false;
                        toDisable.Add(orbit);
                        Debug.Log("[RewardChestUI] OrbitCamera disabled.");
                    }
                }

                _disabledComponents = toDisable.ToArray();
            }
            else
            {
                // Restore PlayerInput
#if ENABLE_INPUT_SYSTEM
                if (_playerInput != null)
                {
                    _playerInput.enabled = true;
                    _playerInput = null;
                    Debug.Log("[RewardChestUI] PlayerInput restored.");
                }
#endif

                // Restore other components
                if (_disabledComponents != null)
                {
                    foreach (var c in _disabledComponents)
                    {
                        if (c != null)
                        {
                            c.enabled = true;
                            Debug.Log($"[RewardChestUI] {c.GetType().Name} restored.");
                        }
                    }
                    _disabledComponents = null;
                }
            }
        }

        /// <summary>
        /// Called when a slot is clicked.
        /// </summary>
        private void OnSlotClicked(RewardItemData item)
        {
            if (!_isOpen) 
            {
                Debug.LogWarning("[RewardChestUI] OnSlotClicked ignored because panel is closed.");
                return;
            }

            Debug.Log($"[RewardChestUI] OnSlotClicked: {item?.ItemName ?? "NULL"}");

            // Cache the callback because ClosePanel wipes it
            var callback = _onSelectionCallback;
            var player = _playerRef;

            ClosePanel();

            if (callback != null)
            {
                Debug.Log("[RewardChestUI] Invoking selection callback.");
                callback.Invoke(item, player);
            }
            else
            {
                Debug.LogError("Callback is null in OnSlotClicked!!");
            }
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

            // Restore input
            TogglePlayerInput(_playerRef, true);
            _playerRef = null;

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
