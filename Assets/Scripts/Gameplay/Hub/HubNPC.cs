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
        [SerializeField] private Sprite portrait;
        [TextArea(3, 5)]
        [SerializeField] private string welcomeText = "Olá como é que estás?. Pronto para mais uma corridinha? HAHAHAH";

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
                if (interactionType == InteractionType.Shop)
                {
                     _interactionUI.StartShopInteraction(currentPlayer, welcomeText, npcName, portrait);
                }
                else
                {
                     _interactionUI.StartInteraction(currentPlayer, welcomeText, npcName, portrait);
                }
            }
            else
            {
                Debug.LogError("Interaction UI not assigned or valid on Hub NPC.");
            }
        }
    }
}
