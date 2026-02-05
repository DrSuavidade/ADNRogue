using UnityEngine;
using UnityEngine.InputSystem;

namespace Geneforge.Gameplay.Hub
{
    public class EssenceLibraryMachine : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string machineName = "DNA Archive";
        [SerializeField] private Sprite portrait;
        [TextArea]
        [SerializeField] private string welcomeText = "Welcome to the Archive. Here you can see all known DNA strains.";

        [Header("References")]
        [SerializeField] private GameObject interactionUIObject; // NPC Manager
        private IHubInteractionUI interactionUI;

        private bool playerInRange;
        private GameObject currentPlayer;

        private void Awake()
        {
            if (interactionUIObject) interactionUI = interactionUIObject.GetComponent<IHubInteractionUI>();
        }
        
        // Simple Interaction Logic (Trigger Enter/Exit/Update)
        private void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) { playerInRange = true; currentPlayer = other.gameObject; } }
        private void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) { playerInRange = false; currentPlayer = null; } }
        private void Update() { if (playerInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Interact(); }

        private void Interact()
        {
            if (interactionUI != null)
            {
                // We need a strictly "Open Library" method, or reuse a generic one
                // Since IHubInteractionUI may not have it yet, we add it!
                interactionUI.StartLibraryInteraction(currentPlayer, welcomeText, machineName, portrait);
            }
        }
    }
}
