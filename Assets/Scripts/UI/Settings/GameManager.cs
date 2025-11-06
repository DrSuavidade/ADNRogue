using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.UI.Settings
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

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

            SaveData data = new SaveData();
            data.level = 1;
            data.coins = 0;
            data.posX = player.position.x;
            data.posY = player.position.y;
            data.posZ = player.position.z;
            data.playTime = playTime;

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
