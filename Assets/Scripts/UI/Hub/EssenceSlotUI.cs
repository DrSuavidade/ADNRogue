using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Geneforge.Gameplay.Abilities;

namespace Geneforge.UI.Hub
{
    public class EssenceSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Button button;
        [SerializeField] private GameObject lockOverlay;

        private AnimalEssence _essence;
        private System.Action<AnimalEssence> _onClick;

        public void Setup(AnimalEssence essence, bool isUnlocked, System.Action<AnimalEssence> onClick)
        {
            _essence = essence;
            _onClick = onClick;

            if (iconImage)
            {
                iconImage.sprite = essence.icon;
                iconImage.color = isUnlocked ? Color.white : Color.black; // Dark if locked
            }

            if (lockOverlay) lockOverlay.SetActive(!isUnlocked);

            if (button)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke(_essence));
            }
        }
    }
}
