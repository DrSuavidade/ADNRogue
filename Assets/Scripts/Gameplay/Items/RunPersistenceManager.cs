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
        private static RunPersistenceManager _instance;
        public static RunPersistenceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<RunPersistenceManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("RunPersistenceManager");
                        _instance = go.AddComponent<RunPersistenceManager>();
                    }
                }
                return _instance;
            }
        }
        
        [SerializeField] private GameSaveData currentData = new GameSaveData();
        
        [Header("Essence Registry")]
        [SerializeField] private List<Geneforge.Gameplay.Abilities.AnimalEssence> allEssences;

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

        public string EquippedPrimary => currentData.equippedPrimaryEssence;
        public List<string> EquippedSecondaries => currentData.equippedSecondaryEssences;

        public void SetEquippedPrimary(string essenceName)
        {
            currentData.equippedPrimaryEssence = essenceName;
            SaveRun();
        }

        public void SetEquippedSecondary(int index, string essenceName)
        {
            if (index < 0 || index >= currentData.equippedSecondaryEssences.Count) return;
            currentData.equippedSecondaryEssences[index] = essenceName;
            SaveRun();
        }

        public Geneforge.Gameplay.Abilities.AnimalEssence GetEssenceByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return allEssences.Find(e => e.name == name || e.displayName == name);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
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

        public void UnlockEssence(string essenceName)
        {
            if (string.IsNullOrEmpty(essenceName)) return;
            if (!currentData.unlockedEssenceIDs.Contains(essenceName))
            {
                currentData.unlockedEssenceIDs.Add(essenceName);
                SaveRun();
                Debug.Log($"[Persistence] Unlocked Essence: {essenceName}");
            }
        }

        public bool IsEssenceUnlocked(string essenceName)
        {
            return currentData.unlockedEssenceIDs.Contains(essenceName);
        }

        public void ClearRun()
        {
            currentData.Clear();
            PersistenceService.DeleteSave();
            Debug.Log("[RunPersistenceManager] Run cleared.");
        }
    }
}
