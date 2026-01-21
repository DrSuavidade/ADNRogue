using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Geneforge.Gameplay.Items
{
    /// <summary>
    /// Pickup that opens a reward selection UI when the player collides with it.
    /// Add this to the chest/reward prefab in the scene.
    /// </summary>
    public class RewardChestPickup : MonoBehaviour
    {
        [Header("Item Pool")]
        [Tooltip("Pool of possible items. 3 random items will be offered to the player.")]
        [SerializeField] private List<RewardItemData> itemPool = new List<RewardItemData>();

        [Header("Settings")]
        [Tooltip("Number of items to offer (default 3).")]
        [SerializeField] private int itemsToOffer = 3;

        [Header("Optional FX Hooks")]
        [SerializeField] private UnityEvent onChestOpened;
        [SerializeField] private UnityEvent onItemSelected;

        private bool _hasBeenUsed = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_hasBeenUsed) return;
            if (!other.CompareTag("Player")) return;

            OpenChest(other.gameObject);
        }

        /// <summary>
        /// Opens the chest and presents the reward selection UI.
        /// </summary>
        private void OpenChest(GameObject player)
        {
            if (_hasBeenUsed) return;
            _hasBeenUsed = true;

            onChestOpened?.Invoke();

            // Get random items from the pool
            List<RewardItemData> offeredItems = GetRandomItems(itemsToOffer);

            // Use the service locator to find the UI provider
            var uiProvider = RewardChestUIService.Provider;
            if (uiProvider != null)
            {
                uiProvider.ShowRewardSelection(offeredItems, player, OnItemChosen);
            }
            else
            {
                Debug.LogWarning("[RewardChestPickup] No RewardChestUI provider registered. Cannot display reward selection.");
                // Fallback: just apply a random item
                if (offeredItems.Count > 0)
                {
                    offeredItems[0].Apply(player);
                }
                CleanupAfterSelection();
            }
        }

        private void OnItemChosen(RewardItemData chosenItem, GameObject player)
        {
            if (chosenItem != null)
            {
                chosenItem.Apply(player);
            }

            onItemSelected?.Invoke();
            CleanupAfterSelection();
        }

        private void CleanupAfterSelection()
        {
            Destroy(gameObject);
        }

        /// <summary>
        /// Get N random unique items from the pool.
        /// </summary>
        private List<RewardItemData> GetRandomItems(int count)
        {
            List<RewardItemData> result = new List<RewardItemData>();
            List<RewardItemData> available = new List<RewardItemData>(itemPool);

            // Remove null entries
            available.RemoveAll(item => item == null);

            count = Mathf.Min(count, available.Count);

            for (int i = 0; i < count; i++)
            {
                int randomIndex = Random.Range(0, available.Count);
                result.Add(available[randomIndex]);
                available.RemoveAt(randomIndex);
            }

            return result;
        }
    }
}

