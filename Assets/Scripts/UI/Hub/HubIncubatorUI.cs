using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Geneforge.Gameplay.Hub;
using System;

namespace Geneforge.UI.Hub
{
    public class HubIncubatorUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonText;
        [SerializeField] private Button closeButton;

        private IncubatorMachine currentMachine;
        private System.Action onCloseCallback;
        private bool isOpen;

        private void Awake()
        {
            if (actionButton) actionButton.onClick.AddListener(OnActionClicked);
            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void Update()
        {
            if (isOpen && currentMachine != null)
            {
                UpdateUIState();
            }
        }

        public void Show(IncubatorMachine machine, System.Action onClose)
        {
            currentMachine = machine;
            onCloseCallback = onClose;
            isOpen = true;
            gameObject.SetActive(true);
            UpdateUIState();
        }

        public void Hide()
        {
            isOpen = false;
            gameObject.SetActive(false);
        }

        private void OnCloseClicked()
        {
            onCloseCallback?.Invoke();
        }

        private void UpdateUIState()
        {
            if (currentMachine == null) return;

            // Currency Update (Using MetaStats)
            int currentSplices = 0;
            if (Geneforge.Core.Stats.MetaStats.Instance != null)
            {
                currentSplices = Geneforge.Core.Stats.MetaStats.Instance.TotalDnaSplices;
            }
            if (currencyText) currencyText.text = $"DNA Splices: {currentSplices}";

            // Machine State
            bool isIncubating = currentMachine.IsIncubating();
            bool isReady = currentMachine.IsReadyToClaim();

            if (isReady)
            {
                if (statusText) statusText.text = "Incubation Complete!";
                if (timerText) timerText.text = "00:00";
                if (actionButtonText) actionButtonText.text = "CLAIM REWARD";
                if (actionButton) actionButton.interactable = true;
            }
            else if (isIncubating)
            {
                if (statusText) statusText.text = "Incubating...";
                TimeSpan remaining = currentMachine.GetTimeRemaining();
                if (timerText) timerText.text = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                if (actionButtonText) actionButtonText.text = "WAIT...";
                if (actionButton) actionButton.interactable = false;
            }
            else
            {
                if (statusText) statusText.text = "Insert DNA to Incubate";
                if (timerText) timerText.text = "--:--";
                if (actionButtonText) actionButtonText.text = $"INCUBATE ({currentMachine.Cost} DNA)";
                
                // Check Affordability via MetaStats
                bool canAfford = currentSplices >= currentMachine.Cost;
                // Debug.Log($"[Incubator Debug] Wallet: {currentSplices} | Cost: {currentMachine.Cost} | CanAfford: {canAfford}");
                
                if (actionButton) actionButton.interactable = canAfford;
            }
        }

        private void OnActionClicked()
        {
            if (currentMachine == null) return;

            if (currentMachine.IsReadyToClaim())
            {
                // Claim silently - Player checks ATM to see what they got!
                var essence = currentMachine.ClaimReward();
                
                // Show generic success message if needed, or just reset
                if (statusText) statusText.text = "DNA Extracted to Archive!";
                
                // Refresh UI to show "Insert DNA" again
                UpdateUIState();
            }
            else if (!currentMachine.IsIncubating())
            {
                // Try Incubate using MetaStats
                var meta = Geneforge.Core.Stats.MetaStats.Instance;
                if (meta != null)
                {
                    if (meta.SpendDnaSplices(currentMachine.Cost))
                    {
                         currentMachine.BeginIncubation();
                         UpdateUIState();
                    }
                    else
                    {
                        Debug.Log("Not enough DNA Splices!");
                    }
                }
            }
        }
    }
}
