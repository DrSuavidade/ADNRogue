using System.IO;
using UnityEngine;

namespace Game.UI.Settings
{
    [System.Serializable]

    public class SaveData
    {
        public string playerName = "Player";
        public int level = 1;
        public int coins = 0;
        public float posX, posY, posZ;
        public float playTime;
        public long savedAt;
    }

    public static class SaveSystem
    {
        private static string Dir => Application.persistentDataPath + "/Saves";
        private static string FilePath(int slot) => $"{Dir}/save_slot{slot}.json";

        public static bool Exists(int slot) => File.Exists(FilePath(slot));

        public static void Save(int slot, SaveData data)
        {
            if (!Directory.Exists(Dir))
                Directory.CreateDirectory(Dir);

            data.savedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath(slot), json);
        }

        public static SaveData Load(int slot)
        {
            if (!Exists(slot)) return null;
            string json = File.ReadAllText(FilePath(slot));
            return JsonUtility.FromJson<SaveData>(json);
        }

        public static void Delete(int slot)
        {
            if (Exists(slot))
                File.Delete(FilePath(slot));
        }
    }
}