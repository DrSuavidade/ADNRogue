using UnityEngine;
using UnityEngine.UI;
using Geneforge.Gameplay.Weapons.Slots;     // GunSlots
using Geneforge.Gameplay.Abilities.Essences; // AnimalEssence

namespace Geneforge.UI
{
    public class GunSlotsHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GunSlots gunSlots;     // Drag Player/Gun object with GunSlots
        [SerializeField] private Image primaryIcon;
        [SerializeField] private Image[] secondaryIcons = new Image[3];

        [Header("Look")]
        [SerializeField] private Sprite emptySprite;
        [SerializeField] private Color filledColor = Color.white;
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.25f);

        void Awake()
        {
            if (gunSlots == null) gunSlots = FindAnyObjectByType<GunSlots>();
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
            var essence = gunSlots != null ? gunSlots.Primary.Essence : null; // Primary slot
            SetIcon(primaryIcon, essence != null ? essence.icon : null);
        }

        void RefreshSecondaries()
        {
            if (secondaryIcons == null) return;
            for (int i = 0; i < secondaryIcons.Length; i++)
            {
                var img = secondaryIcons[i];
                if (img == null) continue;

                var essence = (gunSlots != null && i < gunSlots.Secondaries.Length)
                    ? gunSlots.Secondaries[i].Essence
                    : null;

                SetIcon(img, essence != null ? essence.icon : null);
            }
        }

        void SetIcon(Image img, Sprite sprite)
        {
            img.sprite = sprite != null ? sprite : emptySprite;
            img.color  = (sprite != null) ? filledColor : emptyColor;
        }
    }
}
