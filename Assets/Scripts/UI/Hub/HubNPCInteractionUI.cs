using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Geneforge.Gameplay.Characters.Player;
using Geneforge.Gameplay.Hub;
using Geneforge.Gameplay.Progression;
using Geneforge.Gameplay.Cameras;
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
        [Tooltip("Slower is better. Try 0.2 or 0.3 for a more natural look.")]
        [SerializeField] private float mouthAnimSpeed = 0.25f;

        private GameObject playerRef;
        private enum NextAction { OpenConfirmation, OpenShop, OpenIncubator, OpenLibrary }
        private NextAction nextAction;
        private IncubatorMachine activeIncubatorMachine;
        private OrbitCamera cachedOrbitCamera;
        
        private Coroutine mouthAnimationCoroutine;
        private Sprite[] currentMouthFrames; // [0]=Closed, [1]=Half, [2]=Open

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

        public void StartInteraction(GameObject player, string text, string npcName, Sprite mouthClosed, Sprite mouthHalf, Sprite mouthOpen)
        {
            PrepareInteraction(player);
            nextAction = NextAction.OpenConfirmation;
            ShowDialog(text, npcName, mouthClosed, mouthHalf, mouthOpen);
        }

        public void StartShopInteraction(GameObject player, string text, string npcName, Sprite mouthClosed, Sprite mouthHalf, Sprite mouthOpen)
        {
            PrepareInteraction(player);
            nextAction = NextAction.OpenShop;
            ShowDialog(text, npcName, mouthClosed, mouthHalf, mouthOpen);
        }

        public void StartIncubatorInteraction(GameObject player, string text, string npcName, Sprite mouthClosed, Sprite mouthHalf, Sprite mouthOpen, IncubatorMachine machine)
        {
            PrepareInteraction(player);
            nextAction = NextAction.OpenIncubator;
            activeIncubatorMachine = machine;
            ShowDialog(text, npcName, mouthClosed, mouthHalf, mouthOpen);
        }

        public void StartLibraryInteraction(GameObject player, string text, string npcName, Sprite mouthClosed, Sprite mouthHalf, Sprite mouthOpen)
        {
            PrepareInteraction(player);
            nextAction = NextAction.OpenLibrary;
            ShowDialog(text, npcName, mouthClosed, mouthHalf, mouthOpen);
        }

        private void PrepareInteraction(GameObject player)
        {
            playerRef = player;
            TogglePlayerInput(player, false);
            
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // PRELOAD: Start loading the run scene in the background as soon as we start talking
            if (RunFlowController.Instance != null && !string.IsNullOrEmpty(targetSceneName))
            {
                RunFlowController.Instance.PreloadScene(targetSceneName);
            }
        }

        private void ShowDialog(string text, string name, Sprite mouthClosed, Sprite mouthHalf, Sprite mouthOpen)
        {
            // Reset and stop previous animation
            if (mouthAnimationCoroutine != null) StopCoroutine(mouthAnimationCoroutine);
            currentMouthFrames = null;

            // Close other panels first
            if (confirmationPanel) confirmationPanel.SetActive(false);
            if (shopPanel) shopPanel.Hide();
            if (incubatorPanel) incubatorPanel.Hide();
            if (libraryPanel) libraryPanel.Hide();

            if (dialogPanel)
            {
                dialogPanel.SetActive(true);
                if (dialogText) dialogText.text = ""; // Start empty for typewriter
                
                if (npcNameText) npcNameText.text = name;
                
                if (portraitContainer) portraitContainer.SetActive(mouthClosed != null);
                if (npcPortraitImage)
                {
                    npcPortraitImage.sprite = mouthClosed;
                    npcPortraitImage.enabled = mouthClosed != null;

                    // Setup animation with the provided text
                    if (mouthClosed != null && mouthHalf != null && mouthOpen != null)
                    {
                        currentMouthFrames = new Sprite[] { mouthClosed, mouthHalf, mouthOpen };
                        mouthAnimationCoroutine = StartCoroutine(AnimateMouth(text));
                    }
                    else if (dialogText != null)
                    {
                        dialogText.text = text; // Fallback if no anim
                    }
                }
            }
        }

        private System.Collections.IEnumerator AnimateMouth(string text)
        {
            // Frame timing for 4-step sequence to match ~0.45s per word
            // 0.45 / 4 steps = 0.1125s
            float syllableSpeed = 0.11f; 
            int[] sequence = { 0, 1, 2, 1 };
            int step = 0;

            // Calculation based on user metrics
            string[] words = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            float talkingDuration = words.Length * 0.45f;
            if (talkingDuration < 0.5f) talkingDuration = 0.5f; // Minimum for short text

            while (true)
            {
                // --- TALKING PHASE ---
                float elapsed = 0;
                int charIndex = 0;

                while (elapsed < talkingDuration)
                {
                    // Update Mouth
                    if (npcPortraitImage != null && currentMouthFrames != null)
                    {
                        npcPortraitImage.sprite = currentMouthFrames[sequence[step]];
                    }
                    step = (step + 1) % sequence.Length;

                    // Update Typewriter Text
                    if (dialogText != null)
                    {
                        float progress = elapsed / talkingDuration;
                        charIndex = Mathf.FloorToInt(progress * text.Length);
                        dialogText.text = text.Substring(0, Mathf.Min(charIndex + 1, text.Length));
                    }

                    elapsed += syllableSpeed;
                    yield return new WaitForSecondsRealtime(syllableSpeed);
                }

                // --- FINISH PHASE ---
                if (dialogText != null) dialogText.text = text; // Ensure total text is visible
                if (npcPortraitImage != null && currentMouthFrames != null)
                {
                    npcPortraitImage.sprite = currentMouthFrames[0]; // Fixed Closed Mouth
                }

                // --- IDLE PHASE ---
                // Wait 5 seconds as requested before repeating the sequence
                yield return new WaitForSecondsRealtime(5.0f);
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

            // Deactivate any focus cameras in the scene
            var focusCameras = Object.FindObjectsByType<FocusCamera>(FindObjectsSortMode.None);
            foreach (var fc in focusCameras)
            {
                fc.Deactivate();
            }
        }

        private void HideAll()
        {
            if (mouthAnimationCoroutine != null) StopCoroutine(mouthAnimationCoroutine);
            mouthAnimationCoroutine = null;

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
                if (playerInput == null) playerInput = player.GetComponentInParent<PlayerInput>();
                if (playerInput == null) playerInput = player.GetComponentInChildren<PlayerInput>();
                if (playerInput != null) playerInput.enabled = false;
#endif
                var pc = player.GetComponent<PlayerController>();
                if (pc == null) pc = player.GetComponentInParent<PlayerController>();
                if (pc == null) pc = player.GetComponentInChildren<PlayerController>();
                if (pc != null) pc.enabled = false;

                // Disable Camera
                if (Camera.main != null)
                {
                    cachedOrbitCamera = Camera.main.GetComponent<OrbitCamera>();
                    if (cachedOrbitCamera == null) cachedOrbitCamera = Camera.main.GetComponentInParent<OrbitCamera>();
                    
                    if (cachedOrbitCamera != null) cachedOrbitCamera.enabled = false;
                }
            }
            else
            {
#if ENABLE_INPUT_SYSTEM
                if (playerInput != null) playerInput.enabled = true;
#endif
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = true;

                if (cachedOrbitCamera != null)
                {
                    cachedOrbitCamera.enabled = true;
                    cachedOrbitCamera = null;
                }
            }
        }
    }
}
