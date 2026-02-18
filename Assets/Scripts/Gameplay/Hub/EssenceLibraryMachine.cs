using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace Geneforge.Gameplay.Hub
{
    public class EssenceLibraryMachine : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string machineName = "DNA Archive";
        [SerializeField] private Sprite mouthClosed;
        [SerializeField] private Sprite mouthHalf;
        [SerializeField] private Sprite mouthOpen;
        [TextArea]
        [SerializeField] private string welcomeText = "Welcome to the Archive. Here you can see all known DNA strains.";

        [Header("Camera & Positioning")]
        [SerializeField] private Cameras.FocusCamera focusCam;
        [SerializeField] private Transform playerInteractionPoint;
        [SerializeField] private float positioningSpeed = 5f;

        [Header("References")]
        [SerializeField] private GameObject interactionUIObject; // NPC Manager
        private IHubInteractionUI interactionUI;

        private bool playerInRange;
        private GameObject currentPlayer;

        private void Awake()
        {
            if (interactionUIObject) interactionUI = interactionUIObject.GetComponent<IHubInteractionUI>();
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
                // Do NOT deactivate camera here, let the UI handle it when closed
            } 
        }

        private void Update() 
        { 
            if (playerInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) 
                Interact(); 
        }

        private void Interact()
        {
            if (interactionUI != null && currentPlayer != null)
            {
                // Activate Focus Camera
                if (focusCam != null) focusCam.Activate(currentPlayer.transform);

                // Smoothly reposition player if a point is assigned
                if (playerInteractionPoint != null)
                {
                    StartCoroutine(MovePlayerToPoint(currentPlayer.transform, playerInteractionPoint.position));
                }

                interactionUI.StartLibraryInteraction(currentPlayer, welcomeText, machineName, mouthClosed, mouthHalf, mouthOpen);
            }
        }

        private IEnumerator MovePlayerToPoint(Transform player, Vector3 targetPos)
        {
            float elapsed = 0;
            Vector3 startPos = player.position;
            // Keep player on ground, only move in X and Z
            targetPos.y = startPos.y; 

            while (elapsed < 0.3f)
            {
                if (player == null) yield break;
                player.position = Vector3.Lerp(player.position, targetPos, positioningSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (player != null) player.position = targetPos;
        }
    }
}
