using UnityEngine;
using UnityEngine.UI;
using Geneforge.Gameplay.Weapons.Slots;
using Geneforge.Gameplay.Abilities;

namespace Geneforge.UI
{
    public class GunSlotsHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GunSlots gunSlots;
        [SerializeField] private Image primaryIcon;
        [SerializeField] private Image[] secondaryIcons = new Image[3];

        [Header("Look")]
        [SerializeField] private Sprite emptySprite;
        [SerializeField] private Color filledColor = Color.white;
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.25f);

        void Awake()
        {
            if (gunSlots == null) FindGunSlots();
            SetupDropTargets();
        }

        private void FindGunSlots()
        {
            // 1. Try finding via PlayerController (most reliable)
            var player = FindFirstObjectByType<Geneforge.Gameplay.Characters.Player.PlayerController>();
            if (player != null)
            {
                gunSlots = player.GetComponentInChildren<GunSlots>();
                if (gunSlots != null) return;
            }

            // 2. Try global search including inactive
            var all = Resources.FindObjectsOfTypeAll<GunSlots>();
            if (all != null)
            {
                foreach (var g in all)
                {
                    if (g.gameObject.scene.IsValid())
                    {
                        gunSlots = g;
                        return;
                    }
                }
            }
        }

        void SetupDropTargets()
        {
            // If we still don't have gunSlots, we can't initialize targets properly yet
            // But dragging won't work without it anyway.
            
            SetupDropTarget(primaryIcon, SlotKind.Primary, -1);
            if (secondaryIcons != null)
            {
                for (int i = 0; i < secondaryIcons.Length; i++)
                {
                    SetupDropTarget(secondaryIcons[i], SlotKind.Secondary, i);
                }
            }
        }

        void SetupDropTarget(Image img, SlotKind kind, int index)
        {
            if (img == null) return;
            var target = img.GetComponent<GunSlotDropTarget>();
            if (target == null) target = img.gameObject.AddComponent<GunSlotDropTarget>();
            target.Initialize(gunSlots, kind, index);
        }

        void OnEnable()
        {
            if (gunSlots != null)
            {
                gunSlots.OnPrimaryChanged += HandlePrimaryChanged;      // event lives in GunSlots
                gunSlots.OnSecondariesChanged += HandleSecondariesChanged;
            }
            RefreshAll();
        }

        void OnDisable()
        {
            if (gunSlots != null)
            {
                gunSlots.OnPrimaryChanged -= HandlePrimaryChanged;
                gunSlots.OnSecondariesChanged -= HandleSecondariesChanged;
            }
        }

        void HandlePrimaryChanged(AnimalEssence _)
        {
            RefreshPrimary();
        }

        void HandleSecondariesChanged()
        {
            RefreshSecondaries();
        }

        public void RefreshAll()
        {
            RefreshPrimary();
            RefreshSecondaries();
        }

        void RefreshPrimary()
        {
            if (primaryIcon == null) return;
            var essence = (gunSlots != null && gunSlots.Primary != null)
                ? gunSlots.Primary.Essence
                : null;
            SetIcon(primaryIcon, essence != null ? essence.icon : null);
        }

        void RefreshSecondaries()
        {
            if (secondaryIcons == null) return;
            for (int i = 0; i < secondaryIcons.Length; i++)
            {
                var img = secondaryIcons[i];
                if (img == null) continue;

                var essence = (gunSlots != null && gunSlots.Secondaries != null && i < gunSlots.Secondaries.Length && gunSlots.Secondaries[i] != null)
                    ? gunSlots.Secondaries[i].Essence
                    : null;

                SetIcon(img, essence != null ? essence.icon : null);
            }
        }

        void SetIcon(Image img, Sprite sprite)
        {
            img.sprite = sprite != null ? sprite : emptySprite;
            img.color = (sprite != null) ? filledColor : emptyColor;
        }
    }
}
