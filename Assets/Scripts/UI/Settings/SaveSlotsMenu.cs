using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace Game.UI.Settings
{
    public class SaveSlotsMenu : MonoBehaviour
    {
        [System.Serializable]
        public class SlotUI
        {
            public Button playButton;
            public Button deleteButton;
            public TMP_Text title;
            public TMP_Text subtitle;
            [HideInInspector] public int slotID;
        }

        public SlotUI[] slots = new SlotUI[3];
        public string gameSceneName = "GameScene";

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slotUI = slots[i];     // captura referência à UI
                int s = i + 1;             // slot 1, 2 e 3 (humano)

                slotUI.slotID = s;
                slotUI.title.text = $"FILE {s}";

                bool exists = SaveSystem.Exists(s);
                slotUI.subtitle.text = exists ? "Continuar jogo" : "Vazio";
                slotUI.deleteButton.gameObject.SetActive(exists);

                // limpar listeners antigos
                slotUI.playButton.onClick.RemoveAllListeners();
                slotUI.deleteButton.onClick.RemoveAllListeners();

                // listeners corretos (capturam valor local "s")
                slotUI.playButton.onClick.AddListener(() => OnPlay(s));
                slotUI.deleteButton.onClick.AddListener(() => OnDelete(s));
            }
        }

        private void OnPlay(int slot)
        {
            // Se existe save → carregar
            if (SaveSystem.Exists(slot))
            {
                SaveSlotsGameManager.I.LoadSlot(slot);
            }
            else // se não existe → criar novo
            {
                SaveSlotsGameManager.I.activeSlot = slot;
            }

            // entrar no jogo
            SceneManager.LoadScene(gameSceneName);
        }

        private void OnDelete(int slot)
        {
            SaveSystem.Delete(slot);
            Refresh();
        }
    }
}
