using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Hub;
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
        [SerializeField] private GameObject portraitContainer; // Parent object of the portrait to hide if null

        [Header("Confirmation Panel")]
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private TMP_Text confirmationText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        [Header("Shop Panel")]
        [SerializeField] private HubShopUI shopPanel;

        [Header("Settings")]
        [SerializeField] private string targetSceneName = "WorldGen1";

        private GameObject playerRef;
        private MonoBehaviour[] disabledComponents;
        
        // State to know what the 'Next' button should open
        private enum NextAction { OpenConfirmation, OpenShop }
        private NextAction nextAction;

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

        private void PrepareInteraction(GameObject player)
        {
            playerRef = player;
            TogglePlayerInput(player, false);
            
            // Show Cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void ShowDialog(string text, string name, Sprite portrait)
        {
            if (dialogPanel)
            {
                dialogPanel.SetActive(true);
                if (dialogText) dialogText.text = text;
                
                // Set NPC Details
                if (npcNameText) npcNameText.text = name;
                
                if (portraitContainer) portraitContainer.SetActive(portrait != null);
                if (npcPortraitImage)
                {
                    npcPortraitImage.sprite = portrait;
                    npcPortraitImage.enabled = portrait != null;
                }
            }
            if (confirmationPanel) confirmationPanel.SetActive(false);
            if (shopPanel) shopPanel.Hide();
        }

        private void OnContinueClicked()
        {
            // Close the dialog panel regardless of the next action
            if (dialogPanel) dialogPanel.SetActive(false);

            if (nextAction == NextAction.OpenShop)
            {
                if (shopPanel) 
                {
                   shopPanel.Show(EndInteraction); 
                }
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
            // Restore time/input just in case, though we are loading away
            Time.timeScale = 1f;
            SceneManager.LoadScene(targetSceneName);
        }

        private void OnNoClicked()
        {
            EndInteraction();
        }

        private void EndInteraction()
        {
            HideAll();
            TogglePlayerInput(playerRef, true);
            
            // Hide Cursor usually
             Cursor.visible = false;
             Cursor.lockState = CursorLockMode.Locked;
        }

        private void HideAll()
        {
            if (dialogPanel) dialogPanel.SetActive(false);
            if (confirmationPanel) confirmationPanel.SetActive(false);
            if (shopPanel) shopPanel.Hide();
        }

        private void TogglePlayerInput(GameObject player, bool enabled)
        {
            if (player == null) return;

            if (!enabled)
            {
                // Disable Input
#if ENABLE_INPUT_SYSTEM
                playerInput = player.GetComponent<PlayerInput>();
                if (playerInput != null) playerInput.enabled = false;
#endif
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = false;
            }
            else
            {
                // Enable Input
#if ENABLE_INPUT_SYSTEM
                if (playerInput != null) playerInput.enabled = true;
#endif
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = true;
            }
        }
    }
}
