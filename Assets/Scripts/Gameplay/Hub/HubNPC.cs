using UnityEngine;
using Geneforge.Gameplay.Hub; // for IHubInteractionUI
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Geneforge.Gameplay.Hub
{
    public class HubNPC : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Assign the HubNPCInteractionUI component here.")]
        [SerializeField] private MonoBehaviour interactionUIReference;

        private IHubInteractionUI _interactionUI;

        public enum InteractionType
        {
            StartRun,
            Shop
        }

        [Header("Configuration")]
        [SerializeField] private InteractionType interactionType = InteractionType.StartRun;
        [SerializeField] private string npcName = "Guide";
        
        [Header("Portrait & Animation")]
        [SerializeField] private Sprite mouthClosed;
        [SerializeField] private Sprite mouthHalf;
        [SerializeField] private Sprite mouthOpen;

        [TextArea(3, 5)]
        [SerializeField] private string welcomeText = "Olá como é que estás?. Pronto para mais uma corridinha? HAHAHAH";

        [Header("Camera")]
        [SerializeField] private Cameras.FocusCamera focusCam;

        private bool playerInRange;
        private GameObject currentPlayer;

        private void Awake()
        {
            if (interactionUIReference != null)
            {
                _interactionUI = interactionUIReference as IHubInteractionUI;
                if (_interactionUI == null)
                {
                    Debug.LogError($"Assigned interactionUIReference on {name} does not implement IHubInteractionUI!", this);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                playerInRange = true;
                currentPlayer = other.gameObject;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                playerInRange = false;
                currentPlayer = null;
                // Deactivation is handled by HubNPCInteractionUI when the dialogue finishes
            }
        }

        private void Update()
        {
            if (playerInRange && currentPlayer != null)
            {
                // Check for 'E' key press
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    Interact();
                }
            }
        }

        private void Interact()
        {
            if (_interactionUI != null)
            {
                // Activate Focus Camera
                if (focusCam != null) focusCam.Activate(currentPlayer.transform);

                if (interactionType == InteractionType.Shop)
                {
                     _interactionUI.StartShopInteraction(currentPlayer, welcomeText, npcName, mouthClosed, mouthHalf, mouthOpen);
                }
                else
                {
                     _interactionUI.StartInteraction(currentPlayer, welcomeText, npcName, mouthClosed, mouthHalf, mouthOpen);
                }
            }
            else
            {
                Debug.LogError("Interaction UI not assigned or valid on Hub NPC.");
            }
        }

        public void OnInteractionEnded()
        {
            if (focusCam != null) focusCam.Deactivate();
        }
    }
}
