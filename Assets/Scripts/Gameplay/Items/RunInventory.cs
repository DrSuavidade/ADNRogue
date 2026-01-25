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

        public void AddItem(RewardItemData item)
        {
            if (item == null) return;
            collectedItems.Add(item);
            Debug.Log($"[RunInventory] Collected item: {item.ItemName}. Total items: {collectedItems.Count}");
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
