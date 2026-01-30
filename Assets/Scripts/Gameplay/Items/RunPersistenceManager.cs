using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

namespace Geneforge.Gameplay.Items
{
    /// <summary>
    /// Persists run state (collected items, etc.) to a JSON file.
    /// This allows the run to survive scene changes or game restarts (if resuming).
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
                        // Awake will handle init
                    }
                }
                return _instance;
            }
        }

        [System.Serializable]
        public class SaveData
        {
            public List<string> collectedItemNames = new List<string>();
        }

        [SerializeField] private SaveData currentData = new SaveData();
        private string saveFilePath;

        [Header("Debug")]
        [SerializeField] private bool clearOnStart = false; 

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            saveFilePath = Path.Combine(Application.persistentDataPath, "current_run.json");
            Debug.Log($"<color=green>[RunPersistenceManager] Save file path: {saveFilePath}</color>");
            
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
            Debug.Log($"[RunPersistenceManager] AddItem requested for: {itemName}");
            currentData.collectedItemNames.Add(itemName);
            SaveRun();
        }

        public IReadOnlyList<string> GetCollectedItemNames()
        {
            return currentData.collectedItemNames;
        }

        public void SaveRun()
        {
            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                File.WriteAllText(saveFilePath, json);
                Debug.Log($"<color=cyan>[RunPersistenceManager] Saved run with {currentData.collectedItemNames.Count} items to: {saveFilePath}</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RunPersistenceManager] Failed to save run: {e.Message}");
            }
        }

        public void LoadRun()
        {
            if (!File.Exists(saveFilePath))
            {
                Debug.Log($"[RunPersistenceManager] No save file found at {saveFilePath}. Starting fresh.");
                currentData = new SaveData();
                return;
            }

            try
            {
                string json = File.ReadAllText(saveFilePath);
                currentData = JsonUtility.FromJson<SaveData>(json);
                if (currentData == null) currentData = new SaveData();
                Debug.Log($"<color=green>[RunPersistenceManager] Loaded run with {currentData.collectedItemNames.Count} items.</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RunPersistenceManager] Failed to load run: {e.Message}");
                currentData = new SaveData();
            }
        }

        public void ClearRun()
        {
            currentData = new SaveData();
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
            }
            Debug.Log("[RunPersistenceManager] Run cleared.");
        }
    }
}
