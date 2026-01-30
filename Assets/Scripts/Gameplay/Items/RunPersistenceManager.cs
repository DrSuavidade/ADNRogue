using System.Collections.Generic;
using UnityEngine;
using Geneforge.Core.Persistence;
using Geneforge.Gameplay.Map;

namespace Geneforge.Gameplay.Items
{
    /// <summary>
    /// Manages the runtime state of the current run's items and timeline, using PersistenceService.
    /// </summary>
    public class RunPersistenceManager : MonoBehaviour
    {
        public static RunPersistenceManager Instance { get; private set; }
        
        [SerializeField] private GameSaveData currentData = new GameSaveData();
        
        [Header("Debug")]
        [SerializeField] private bool clearOnStart = false; 

        public TimelineId CurrentTimeline
        {
            get => (TimelineId)currentData.currentTimelineId;
            set
            {
                currentData.currentTimelineId = (int)value;
                SaveRun();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (clearOnStart)
            {
                ClearRun();
            }
            else
            {
                LoadRun();
            }
        }

        public void AddItem(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return;
            
            Debug.Log($"[RunPersistenceManager] AddItem: {itemName}");
            currentData.collectedItemNames.Add(itemName);
            SaveRun();
        }

        public IReadOnlyList<string> GetCollectedItemNames()
        {
            return currentData.collectedItemNames;
        }

        public void SaveRun()
        {
            PersistenceService.Save(currentData);
        }

        public void LoadRun()
        {
            currentData = PersistenceService.Load();
            Debug.Log($"[RunPersistenceManager] Loaded {currentData.collectedItemNames.Count} items. Timeline: {CurrentTimeline}");
        }

        public void ClearRun()
        {
            currentData.Clear();
            PersistenceService.DeleteSave();
            Debug.Log("[RunPersistenceManager] Run cleared.");
        }
    }
}
