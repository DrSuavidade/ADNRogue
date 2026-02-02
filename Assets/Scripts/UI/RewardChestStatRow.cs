using UnityEngine;
using UnityEngine.UI;

namespace Geneforge.UI
{
    public class RewardChestStatRow : MonoBehaviour
    {
        [SerializeField] private Image statIcon;
        [SerializeField] private Image arrowIcon;
        [SerializeField] private Color upgradeColor = Color.green;
        [SerializeField] private Color downgradeColor = Color.red;

        public void Setup(Sprite icon, Sprite arrow, bool isUpgrade)
        {
            if (statIcon != null) 
            {
                statIcon.sprite = icon;
                statIcon.enabled = icon != null;
            }
            
            if (arrowIcon != null) 
            {
                arrowIcon.sprite = arrow;
                arrowIcon.color = isUpgrade ? upgradeColor : downgradeColor;
                arrowIcon.enabled = arrow != null;
            }
        }
    }
}
