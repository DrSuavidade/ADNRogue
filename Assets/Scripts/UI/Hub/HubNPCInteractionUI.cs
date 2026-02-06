using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Hub;
using Geneforge.Gameplay.Progression;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Geneforge.UI.Hub
{
    public class HubNPCInteractionUI : MonoBehaviour, IHubInteractionUI
    {
        [Header("Dialog Panel")]
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private TMP_Text dialogText;
        [SerializeField] private Button dialogContinueButton;
        
        [Header("NPC Display")]
        [SerializeField] private Image npcPortraitImage;
        [SerializeField] private TMP_Text npcNameText;
        [SerializeField] private GameObject portraitContainer;

        [Header("Confirmation Panel")]
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private TMP_Text confirmationText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        [Header("Shop Panel")]
        [SerializeField] private HubShopUI shopPanel;

        [Header("Incubator Panel")]
        [SerializeField] private HubIncubatorUI incubatorPanel;

        [Header("Library Panel")]
        [SerializeField] private EssenceLibraryUI libraryPanel;

        [Header("Settings")]
        [SerializeField] private string targetSceneName = "WorldGen1";

        private GameObject playerRef;
        private enum NextAction { OpenConfirmation, OpenShop, OpenIncubator, OpenLibrary }
        private NextAction nextAction;
        private IncubatorMachine activeIncubatorMachine;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput playerInput;
#endif

        private void Awake()
        {
            if (dialogContinueButton) dialogContinueButton.onClick.AddListener(OnContinueClicked);
            if (yesButton) yesButton.onClick.AddListener(OnYesClicked);
            if (noButton) noButton.onClick.AddListener(OnNoClicked);

            HideAll();
        }

        public void StartInteraction(GameObject player, string text, string npcName, Sprite portrait)
        {
            PrepareInteraction(player);
            nextAction = NextAction.OpenConfirmation;
            ShowDialog(text, npcName, portrait);
        }

        public void StartShopInteraction(GameObject player, string text, string npcName, Sprite portrait)
        {
            PrepareInteraction(player);
            nextAction = NextAction.OpenShop;
            ShowDialog(text, npcName, portrait);
        }

        public void StartIncubatorInteraction(GameObject player, string text, string npcName, Sprite portrait, IncubatorMachine machine)
        {
            PrepareInteraction(player);
            nextAction = NextAction.OpenIncubator;
            activeIncubatorMachine = machine;
            ShowDialog(text, npcName, portrait);
        }

        public void StartLibraryInteraction(GameObject player, string text, string npcName, Sprite portrait)
        {
            PrepareInteraction(player);
            nextAction = NextAction.OpenLibrary;
            ShowDialog(text, npcName, portrait);
        }

        private void PrepareInteraction(GameObject player)
        {
            playerRef = player;
            TogglePlayerInput(player, false);
            
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void ShowDialog(string text, string name, Sprite portrait)
        {
            // Close other panels first
            if (confirmationPanel) confirmationPanel.SetActive(false);
            if (shopPanel) shopPanel.Hide();
            if (incubatorPanel) incubatorPanel.Hide();
            if (libraryPanel) libraryPanel.Hide();

            if (dialogPanel)
            {
                dialogPanel.SetActive(true);
                if (dialogText) dialogText.text = text;
                
                if (npcNameText) npcNameText.text = name;
                
                if (portraitContainer) portraitContainer.SetActive(portrait != null);
                if (npcPortraitImage)
                {
                    npcPortraitImage.sprite = portrait;
                    npcPortraitImage.enabled = portrait != null;
                }
            }
        }

        private void OnContinueClicked()
        {
            if (dialogPanel) dialogPanel.SetActive(false);

            if (nextAction == NextAction.OpenShop)
            {
                if (shopPanel) shopPanel.Show(EndInteraction); 
            }
            else if (nextAction == NextAction.OpenIncubator)
            {
                if (incubatorPanel) incubatorPanel.Show(activeIncubatorMachine, EndInteraction);
            }
            else if (nextAction == NextAction.OpenLibrary)
            {
                if (libraryPanel) libraryPanel.Show(EndInteraction);
            }
            else
            {
                ShowConfirmation();
            }
        }

        private void ShowConfirmation()
        {
            if (confirmationPanel)
            {
                confirmationPanel.SetActive(true);
                if (confirmationText) confirmationText.text = "Deseja enviar um clone para a run?";
            }
        }

        private void OnYesClicked()
        {
            // Visual feedback so the player knows it's working
            if (yesButton) yesButton.interactable = false;
            if (noButton) noButton.interactable = false;
            if (confirmationText) confirmationText.text = "A gerar mundo... Por favor aguarde.";

            Time.timeScale = 1f;
            
            // Try to use the flow controller to properly reset/sync resources
            if (RunFlowController.Instance != null)
            {
                RunFlowController.Instance.StartNewRun();
            }
            else
            {
                // Fallback for direct testing
                SceneManager.LoadSceneAsync(targetSceneName);
            }
        }

        private void OnNoClicked()
        {
            EndInteraction();
        }

        private void EndInteraction()
        {
            HideAll();
            TogglePlayerInput(playerRef, true);
            
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void HideAll()
        {
            if (dialogPanel) dialogPanel.SetActive(false);
            if (confirmationPanel) confirmationPanel.SetActive(false);
            if (shopPanel) shopPanel.Hide();
            if (incubatorPanel) incubatorPanel.Hide();
            if (libraryPanel) libraryPanel.Hide();
        }

        private void TogglePlayerInput(GameObject player, bool enabled)
        {
            if (player == null) return;

            if (!enabled)
            {
#if ENABLE_INPUT_SYSTEM
                playerInput = player.GetComponent<PlayerInput>();
                if (playerInput != null) playerInput.enabled = false;
#endif
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = false;
            }
            else
            {
#if ENABLE_INPUT_SYSTEM
                if (playerInput != null) playerInput.enabled = true;
#endif
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = true;
            }
        }
    }
}
