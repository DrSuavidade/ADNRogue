using UnityEngine;
using UnityEngine.EventSystems;
using Geneforge.Gameplay.Weapons.Slots;
using Geneforge.Gameplay.Abilities;
using Geneforge.UI.Hub;

namespace Geneforge.UI
{
    public class GunSlotDropTarget : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        private GunSlots _gunSlots;
        private SlotKind _kind;
        private int _index;

        public void Initialize(GunSlots gunSlots, SlotKind kind, int index)
        {
            _gunSlots = gunSlots;
            _kind = kind;
            _index = index;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                EnsureGunSlots();
                if (_gunSlots == null) return;

                if (_kind == SlotKind.Primary)
                {
                    _gunSlots.ClearPrimary();
                }
                else
                {
                    _gunSlots.ClearSecondary(_index);
                }
            }
        }

        private void EnsureGunSlots()
        {
            if (_gunSlots == null)
            {
                // Fallback: Try to find GunSlots including inactive objects
                var all = Resources.FindObjectsOfTypeAll<GunSlots>();
                if (all != null && all.Length > 0)
                {
                    foreach (var g in all)
                    {
                        if (g.gameObject.scene.IsValid())
                        {
                            _gunSlots = g;
                            break;
                        }
                    }
                }
            }
        }

        public void OnDrop(PointerEventData data)
        {
            EnsureGunSlots();
            if (_gunSlots == null) return;

            var dragObj = data.pointerDrag;
            if (dragObj == null) return;

            // Try to get EssenceSlotUI from the dragged object
            var essenceSlot = dragObj.GetComponent<EssenceSlotUI>();
            if (essenceSlot != null && essenceSlot.Essence != null)
            {
                if (_kind == SlotKind.Primary)
                {
                    _gunSlots.TrySetPrimary(essenceSlot.Essence);
                }
                else
                {
                    _gunSlots.TrySetSecondary(_index, essenceSlot.Essence);
                }
            }
        }
    }
}
