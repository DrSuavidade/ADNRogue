using UnityEngine;
using Geneforge.Gameplay.Map;

namespace Geneforge.UI
{
    public class KeyHUD : MonoBehaviour
    {
        [Tooltip("Root GameObject (icon) to show/hide. If null, this GameObject is used.")]
        [SerializeField] private GameObject keyIconRoot;

        private bool _subscribed;

        private void Awake()
        {
            if (keyIconRoot == null)
                keyIconRoot = gameObject;

            // Safe default
            if (keyIconRoot != null)
                keyIconRoot.SetActive(false);
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Update()
        {
            // Handles the case where DungeonMapManager.Instance appears AFTER this UI is enabled
            if (!_subscribed)
                TrySubscribe();
        }

        private void OnDisable()
        {
            if (!_subscribed) return;

            var mgr = DungeonMapManager.Instance;
            if (mgr != null)
                mgr.KeyStateChanged -= OnKeyStateChanged;

            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;

            var mgr = DungeonMapManager.Instance;
            if (mgr == null) return;

            mgr.KeyStateChanged += OnKeyStateChanged;
            _subscribed = true;

            // Sync with current state immediately
            OnKeyStateChanged(mgr.PlayerHasKey);
        }

        private void OnKeyStateChanged(bool hasKey)
        {
            if (keyIconRoot != null)
                keyIconRoot.SetActive(hasKey);
        }
    }
}
