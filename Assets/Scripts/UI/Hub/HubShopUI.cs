using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Geneforge.UI.Hub
{
    public class HubShopUI : MonoBehaviour
    {
        [Header("Shop Panels")]
        [SerializeField] private GameObject buyPanel;
        [SerializeField] private GameObject sellPanel;

        [Header("Buttons")]
        [SerializeField] private Button buyTabButton;
        [SerializeField] private Button sellTabButton;
        [SerializeField] private Button closeButton;

        [Header("Currency")]
        [SerializeField] private TMP_Text currencyText;

        [Header("Content")]
        [SerializeField] private RectTransform buyItemsContainer;
        [SerializeField] private ShopSlotUI slotPrefab; 

        [Header("TestData")]
        [SerializeField] private Geneforge.Gameplay.Items.RewardItemData[] testItemsToSell;

        private System.Action onClose;

        private void Awake()
        {
            if (buyTabButton) buyTabButton.onClick.AddListener(ShowBuyPanel);
            if (sellTabButton) sellTabButton.onClick.AddListener(ShowSellPanel);
            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
        }

        public void Show(System.Action closeCallback)
        {
            this.gameObject.SetActive(true);
            onClose = closeCallback;
            ShowBuyPanel();
            UpdateCurrencyDisplay();
            PopulateShop();
        }

        public void Hide()
        {
            // Optional: Clear items when hiding so they don't stay there
            foreach (Transform child in buyItemsContainer)
            {
                Destroy(child.gameObject);
            }
            this.gameObject.SetActive(false);
        }

        public void ShowBuyPanel()
        {
            if (buyPanel) buyPanel.SetActive(true);
            if (sellPanel) sellPanel.SetActive(false);
        }

        public void ShowSellPanel()
        {
            if (buyPanel) buyPanel.SetActive(false);
            if (sellPanel) sellPanel.SetActive(true);
        }

        private void OnCloseClicked()
        {
            Hide();
            onClose?.Invoke();
        }

        private void UpdateCurrencyDisplay()
        {
            if (currencyText)
            {
                if (Geneforge.Gameplay.Progression.RunSession.Instance != null && 
                    Geneforge.Gameplay.Progression.RunSession.Instance.Run != null)
                {
                    int currentGold = Geneforge.Gameplay.Progression.RunSession.Instance.Run.Gold;
                    currencyText.text = $"GOLD: {currentGold}";
                }
                else
                {
                    currencyText.text = "GOLD: 0";
                }
            }
        }

        private void PopulateShop()
        {
            foreach (Transform child in buyItemsContainer)
            {
                Destroy(child.gameObject);
            }

            if (testItemsToSell != null)
            {
                foreach (var item in testItemsToSell)
                {
                    if (item == null) continue;
                    
                    var slotObj = Instantiate(slotPrefab, buyItemsContainer);
                    int price = 50; 

                    slotObj.Setup(item, price, "BUY", (clickedItem) => {
                        TryBuyItem(clickedItem, price);
                    });
                }
            }
        }

        private void TryBuyItem(Geneforge.Gameplay.Items.RewardItemData item, int cost)
        {
            var session = Geneforge.Gameplay.Progression.RunSession.Instance;
            if (session != null && session.Run != null)
            {
                if (session.Run.SpendGold(cost))
                {
                    Debug.Log($"Purchased {item.ItemName} for {cost}!");
                    UpdateCurrencyDisplay();
                    // Lógica para adicionar item ao inventário pode vir aqui
                }
                else
                {
                    Debug.Log("Not enough gold!");
                }
            }
        }
    }
}
