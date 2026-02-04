using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Geneforge.Gameplay.Items;

namespace Geneforge.UI.Hub
{
    public class ShopSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button actionButton; // Buy or Sell button

        private RewardItemData _item;
        private System.Action<RewardItemData> _callback;

        public void Setup(RewardItemData item, int price, string actionLabel, System.Action<RewardItemData> callback)
        {
            _item = item;
            _callback = callback;

            if (item != null)
            {
                if (nameText) nameText.text = item.ItemName;
                if (iconImage) 
                {
                    iconImage.sprite = item.Icon;
                    iconImage.enabled = item.Icon != null;
                }
            }

            if (priceText) priceText.text = $"{price} GOLD";
            
            // Setup Button
            if (actionButton)
            {
                var btnText = actionButton.GetComponentInChildren<TMP_Text>();
                if (btnText) btnText.text = actionLabel;
                
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            _callback?.Invoke(_item);
        }
    }
}
