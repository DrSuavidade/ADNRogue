using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Geneforge.Gameplay.Abilities;
using Geneforge.Gameplay.Items;

namespace Geneforge.UI.Hub
{
    public class EssenceLibraryUI : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private List<AnimalEssence> allEssences; // Drag ALL essences here manually
        [SerializeField] private EssenceSlotUI slotPrefab;
        [SerializeField] private Transform gridContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailDescription;
        [SerializeField] private Image detailIcon;

        private System.Action onClose;

        private void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(CloseLibrary);
            if (detailPanel) detailPanel.SetActive(false);
        }

        public void Show(System.Action closeCallback)
        {
            onClose = closeCallback;
            gameObject.SetActive(true);
            PopulateGrid();
            // Select first if available
            if (allEssences.Count > 0) ShowDetail(allEssences[0]);
        }

        public void CloseLibrary()
        {
            gameObject.SetActive(false);
            onClose?.Invoke();
            onClose = null; // Clear to prevent double calls
        }

        public void Hide()
        {
            onClose = null;
            gameObject.SetActive(false);
        }

        private void PopulateGrid()
        {
            // Clear old
            foreach (Transform child in gridContainer) Destroy(child.gameObject);

            var manager = RunPersistenceManager.Instance;
            
            foreach (var essence in allEssences)
            {
                if (essence == null) continue;

                var slot = Instantiate(slotPrefab, gridContainer);
                bool isUnlocked = manager != null && manager.IsEssenceUnlocked(essence.name);

                slot.Setup(essence, isUnlocked, (clickedEssence) => {
                    ShowDetail(clickedEssence);
                });
            }
        }

        private void ShowDetail(AnimalEssence essence)
        {
            var manager = RunPersistenceManager.Instance;
            bool isUnlocked = manager != null && manager.IsEssenceUnlocked(essence.name);

            if (detailPanel) detailPanel.SetActive(true);
            
            if (isUnlocked)
            {
                if (detailName) detailName.text = essence.displayName;
                if (detailDescription) detailDescription.text = essence.description;
                if (detailIcon) 
                {
                    detailIcon.sprite = essence.icon;
                    detailIcon.color = Color.white;
                }
            }
            else
            {
                if (detailName) detailName.text = "???";
                if (detailDescription) detailDescription.text = "This essence has not been discovered yet.";
                if (detailIcon) 
                {
                    detailIcon.sprite = essence.icon; // Or a locked sprite
                    detailIcon.color = Color.black; // Silhouette
                }
            }
        }
    }
}
