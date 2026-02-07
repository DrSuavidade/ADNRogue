using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Geneforge.Gameplay.Abilities;

namespace Geneforge.UI.Hub
{
    public class EssenceSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Button button;
        [SerializeField] private GameObject lockOverlay;

        private AnimalEssence _essence;
        private System.Action<AnimalEssence> _onClick;
        
        public AnimalEssence Essence => _essence;

        private GameObject _dragObj;

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

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_essence == null) return;
            
            // Create a drag visual
            _dragObj = new GameObject("DragIcon");
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _dragObj.transform.SetParent(canvas.rootCanvas.transform); // Use root canvas to be on top
                _dragObj.transform.SetAsLastSibling();
            }
            else
            {
                _dragObj.transform.SetParent(transform.parent);
            }

            var img = _dragObj.AddComponent<Image>();
            img.sprite = _essence.icon;
            img.preserveAspect = true;
            img.raycastTarget = false; // Important: let rays pass through to the drop target

            // Copy size
            var rt = _dragObj.GetComponent<RectTransform>();
            var sourceRt = iconImage ? iconImage.rectTransform : GetComponent<RectTransform>();
            if (rt && sourceRt) rt.sizeDelta = sourceRt.sizeDelta;

            UpdateDragPos(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateDragPos(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_dragObj != null) Destroy(_dragObj);
        }

        private void UpdateDragPos(PointerEventData data)
        {
            if (_dragObj != null) _dragObj.transform.position = data.position;
        }
    }
}
