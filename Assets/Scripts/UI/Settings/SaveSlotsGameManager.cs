using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.Settings
{
    public class SaveSlotsGameManager : MonoBehaviour
    {
        public static SaveSlotsGameManager I;

        public int activeSlot = -1;
        public Transform player;
        public float playTime;

        private void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (activeSlot != -1)
                playTime += Time.deltaTime;
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoad;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoad;

        void OnSceneLoad(Scene scene, LoadSceneMode mode)
        {
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) player = p.transform;
            }
        }

        public void SaveNow()
        {
            if (activeSlot < 1) return;

            if (player == null)
            {
                Debug.LogWarning("SaveSlotsGameManager.SaveNow called but player reference is null.", this);
                return;
            }

            var data = new SaveData
            {
                level = 1,
                coins = 0,
                posX = player.position.x,
                posY = player.position.y,
                posZ = player.position.z,
                playTime = playTime
            };

            SaveSystem.Save(activeSlot, data);
        }

        public bool LoadSlot(int slot)
        {
            SaveData data = SaveSystem.Load(slot);
            if (data == null) return false;

            activeSlot = slot;
            playTime = data.playTime;

            if (player != null)
                player.position = new Vector3(data.posX, data.posY, data.posZ);

            return true;
        }
    }
}
