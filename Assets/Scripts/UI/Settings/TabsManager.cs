using UnityEngine;
using UnityEngine.UI;

namespace Geneforge.UI
{
    public class TabsManager : MonoBehaviour
    {
        [System.Serializable]
        public class Tab
        {
            public Button button;        // botão da tab (GAME, VIDEO, etc.)
            public GameObject content;   // painel correspondente (GameContent, VideoContent, etc.)
        }

        public Tab[] tabs;
        private int current = -1;

        void Start()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var tab = tabs[i];
                if (tab == null || tab.button == null || tab.content == null)
                {
                    Debug.LogWarning($"TabsManager: Tab {i} is missing button or content.", this);
                    continue;
                }
                tab.button.onClick.AddListener(() => Select(index));
            }

            if (tabs.Length > 0)
                Select(0);
        }

        public void Select(int index)
        {
            // ativa só o painel da tab escolhida
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = (i == index);
                tabs[i].content.SetActive(active);
            }

            current = index;
        }
    }
}