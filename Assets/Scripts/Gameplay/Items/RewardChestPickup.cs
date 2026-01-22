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

            Debug.Log($"[RewardChestPickup] Opening chest for {player.name}");
            onChestOpened?.Invoke();

            // Get random items from the pool
            List<RewardItemData> offeredItems = GetRandomItems(itemsToOffer);
            Debug.Log($"[RewardChestPickup] Found {offeredItems.Count} items to offer.");

            // Use the service locator to find the UI provider
            var uiProvider = RewardChestUIService.Provider;
            
            // Fallback: Safe way to find the interface without triggering Unity's Object constraint
            if (uiProvider == null)
            {
                // We find all MonoBehaviours and look for one that implements the interface.
                // This is a bit slower but only happens once if the service locator fails.
                var allBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var b in allBehaviours)
                {
                    if (b is IRewardChestUIProvider provider)
                    {
                        Debug.Log($"[RewardChestPickup] Found provider in object: {b.name}. Registering now.");
                        RewardChestUIService.Register(provider);
                        uiProvider = provider;
                        break;
                    }
                }
            }

            if (uiProvider != null && offeredItems.Count > 0)
            {
                uiProvider.ShowRewardSelection(offeredItems, player, OnItemChosen);
            }
            else
            {
                if (uiProvider == null)
                    Debug.LogWarning("[RewardChestPickup] No RewardChestUI provider found! Did you add the RewardChestUI script to the scene?");
                if (offeredItems.Count == 0)
                    Debug.LogWarning("[RewardChestPickup] Item list is empty! Check your DungeonConfig Global Item Pool and Item Rarities.");

                // Fallback: just apply a random item if any
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
            List<RewardItemData> available = new List<RewardItemData>();

            // GET ITEMS FROM THE GLOBAL POOL MANAGED BY DUNGEON MAP MANAGER WITH RARITY WEIGHTS
            if (Map.DungeonMapManager.Instance != null)
            {
                return Map.DungeonMapManager.Instance.GetWeightedRandomRewardItems(count);
            }

            // Fallback to local pool if provided (only if Map manager is missing)
            if (itemPool != null && itemPool.Count > 0)
            {
                available.AddRange(itemPool);
            }

            // Remove null entries
            available.RemoveAll(item => item == null);

            List<RewardItemData> result = new List<RewardItemData>();
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

