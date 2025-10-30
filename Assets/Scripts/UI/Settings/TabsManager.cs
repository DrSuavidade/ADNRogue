using UnityEngine;
using UnityEngine.UI;


namespace Geneforge.UI
{
public class TabsManager : MonoBehaviour
{
    [System.Serializable]   // <- isto é obrigatório para aparecer no Inspector
    public class Tab
    {
        public Button button;        // botão da tab (GAME, VIDEO, etc.)
        public GameObject content;   // painel correspondente (GameContent, VideoContent, etc.)
    }

    public Tab[] tabs; // esta lista aparece no Inspector
    private int current = -1;

    void Start()
    {
        // liga cada botão ao método Select
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].button.onClick.AddListener(() => Select(index));
        }

        // ativa a primeira tab por defeito
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