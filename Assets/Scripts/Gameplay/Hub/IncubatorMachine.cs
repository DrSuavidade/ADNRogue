using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using Geneforge.Gameplay.Abilities;
using Geneforge.Core.Stats;

namespace Geneforge.Gameplay.Hub
{
    public class IncubatorMachine : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string machineName = "DNA Incubator";
        [SerializeField] private Sprite mouthClosed;
        [SerializeField] private Sprite mouthHalf;
        [SerializeField] private Sprite mouthOpen;
        [TextArea(3, 5)]
        [SerializeField] private string welcomeText = "Olá, bem vindo à parte do DNA! Vamos incubar o teu DNA.";
        [SerializeField] private int incubationCost = 5;
        [SerializeField] private float incubationTimeMinutes = 5f;
        
        [Header("Rewards")]
        [SerializeField] private List<AnimalEssence> essencePool;

        [Header("References")]
        [SerializeField] private GameObject interactionUIObject;
        private IHubInteractionUI interactionUI;

        private bool playerInRange;
        private GameObject currentPlayer;

        // Persistence Keys
        private const string KEY_START_TIME = "Incubator_StartTime";
        private const string KEY_IS_INCUBATING = "Incubator_IsIncubating";

        public int Cost => incubationCost;

        private void Awake()
        {
            if (interactionUIObject != null)
            {
                interactionUI = interactionUIObject.GetComponent<IHubInteractionUI>();
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
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    Interact();
                }
            }
        }

        private void Interact()
        {
            if (interactionUI != null)
            {
                interactionUI.StartIncubatorInteraction(currentPlayer, welcomeText, machineName, mouthClosed, mouthHalf, mouthOpen, this);
            }
        }

        public bool IsIncubating()
        {
            return PlayerPrefs.GetInt(KEY_IS_INCUBATING, 0) == 1;
        }

        public DateTime GetStartTime()
        {
            string ticksStr = PlayerPrefs.GetString(KEY_START_TIME, "0");
            long ticks = long.Parse(ticksStr);
            return new DateTime(ticks);
        }

        public TimeSpan GetTimeRemaining()
        {
            if (!IsIncubating()) return TimeSpan.Zero;
            
            DateTime start = GetStartTime();
            DateTime end = start.AddMinutes(incubationTimeMinutes);
            TimeSpan remaining = end - DateTime.Now;
            
            return remaining.TotalSeconds > 0 ? remaining : TimeSpan.Zero;
        }

        public bool IsReadyToClaim()
        {
            return IsIncubating() && GetTimeRemaining() <= TimeSpan.Zero;
        }

        public void BeginIncubation()
        {
            // Deduct Logic (assuming RunStats/MetaStats has DnaSplices)
            // For now, checks are done by UI or logic before calling this.
            
            PlayerPrefs.SetInt(KEY_IS_INCUBATING, 1);
            PlayerPrefs.SetString(KEY_START_TIME, DateTime.Now.Ticks.ToString());
            PlayerPrefs.Save();
        }

        public AnimalEssence ClaimReward()
        {
            if (essencePool == null || essencePool.Count == 0) return null;

            // Reset State
            PlayerPrefs.SetInt(KEY_IS_INCUBATING, 0);
            PlayerPrefs.Save();

            // Pick Random
            int index = UnityEngine.Random.Range(0, essencePool.Count);
            var reward = essencePool[index];
            
            // Persist Unlock
            if (reward != null)
            {
                var mgr = Geneforge.Gameplay.Items.RunPersistenceManager.Instance;
                if (mgr != null) mgr.UnlockEssence(reward.name);
            }

            return reward;
        }
    }
}
