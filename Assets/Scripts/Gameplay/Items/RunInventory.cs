using UnityEngine;
using System.Collections.Generic;

namespace Geneforge.Gameplay.Items
{
    /// <summary>
    /// Keeps track of all items collected during the current run.
    /// Attached to the Player GameObject.
    /// </summary>
    public class RunInventory : MonoBehaviour
    {
        [Tooltip("Items collected in this run.")]
        [SerializeField]
        private List<RewardItemData> collectedItems = new List<RewardItemData>();

        public IReadOnlyList<RewardItemData> CollectedItems => collectedItems;

        private bool _isRestoring = false;

        private void Start()
        {
            RestoreCollectedItems();
        }

        public void AddItem(RewardItemData item)
        {
            if (item == null) return;
            
            collectedItems.Add(item);
            Debug.Log($"[RunInventory] Collected item: {item.ItemName}. Total items: {collectedItems.Count}");

            // Only save if we are not currently restoring from save file (to avoid duplication/recursion)
            if (!_isRestoring && RunPersistenceManager.Instance != null)
            {
                RunPersistenceManager.Instance.AddItem(item.ItemName);
            }
        }

        private void RestoreCollectedItems()
        {
            if (RunPersistenceManager.Instance == null) return;
            if (Map.DungeonMapManager.Instance == null) return;

            var savedNames = RunPersistenceManager.Instance.GetCollectedItemNames();
            if (savedNames == null || savedNames.Count == 0) return;

            Debug.Log($"[RunInventory] Restoring {savedNames.Count} items...");
            _isRestoring = true;

            foreach (var name in savedNames)
            {
                var itemData = Map.DungeonMapManager.Instance.GetRewardItemByName(name);
                if (itemData != null)
                {
                    // Re-apply the item to this player instance.
                    // Note: RewardItemData.Apply() calls inventory.AddItem(), so we use _isRestoring to handle that call.
                    itemData.Apply(gameObject);
                }
                else
                {
                    Debug.LogWarning($"[RunInventory] Could not find item with name '{name}' in global pool. Skipping.");
                }
            }

            _isRestoring = false;
        }

        public bool HasItem(RewardItemData item)
        {
            return collectedItems.Contains(item);
        }
        
        public int GetItemCount(RewardItemData item)
        {
            int count = 0;
            foreach(var i in collectedItems)
            {
                if(i == item) count++;
            }
            return count;
        }
    }
}
