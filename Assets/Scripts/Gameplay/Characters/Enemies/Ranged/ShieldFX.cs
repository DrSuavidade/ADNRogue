using UnityEngine;

namespace Geneforge.Gameplay.Characters.Enemies.Ranged
{
    public class ShieldFX : MonoBehaviour
    {
        [Header("Shield FX")]
        public GameObject shieldPrefab;
        public Transform attachPoint;   // onde o shield aparece (frente do boneco)

        GameObject _currentShield;

        // Ligar ao OnBlockStart (no RangedMagic)
        public void ShowShield()
        {
            if (shieldPrefab == null || attachPoint == null) return;

            if (_currentShield == null)
            {
                _currentShield = Instantiate(
                    shieldPrefab,
                    attachPoint.position,
                    attachPoint.rotation,
                    attachPoint
                );
            }

            _currentShield.SetActive(true);
        }

        // Ligar ao OnBlockEnd (no RangedMagic)
        public void HideShield()
        {
            if (_currentShield != null)
                _currentShield.SetActive(false);
        }
    }
}
