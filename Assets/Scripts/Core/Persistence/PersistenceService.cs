using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Geneforge.Core.Persistence
{
    /// <summary>
    /// handle file I/O for persistence.
    /// Uses JSON serialization.
    /// </summary>
    public static class PersistenceService
    {
        private const string SAVE_FILE_NAME = "current_run.json";

        private static string FilePath => Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        public static void Save(GameSaveData data)
        {
            if (data == null) return;
            
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(FilePath, json);
                Debug.Log($"[PersistenceService] Saved to {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PersistenceService] Save failed: {e.Message}");
            }
        }
        
        public static async Task SaveAsync(GameSaveData data)
        {
             if (data == null) return;
             
             try
             {
                 string json = JsonUtility.ToJson(data, true);
                 await File.WriteAllTextAsync(FilePath, json);
                 Debug.Log($"[PersistenceService] Saved Async to {FilePath}");
             }
             catch (Exception e)
             {
                 Debug.LogError($"[PersistenceService] Save Async failed: {e.Message}");
             }
        }

        public static GameSaveData Load()
        {
            if (!File.Exists(FilePath))
            {
                return new GameSaveData();
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<GameSaveData>(json);
                return data ?? new GameSaveData();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PersistenceService] Load failed: {e.Message}");
                return new GameSaveData();
            }
        }

        public static void DeleteSave()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
                Debug.Log("[PersistenceService] Save file deleted.");
            }
        }
        
        public static bool SaveExists() => File.Exists(FilePath);
    }
}
