using System.Collections;
using UnityEngine;
using Geneforge.Gameplay.Map;

namespace Geneforge.UI
{
    public class KeyHUD : MonoBehaviour
    {
        [Tooltip("Root GameObject (icon) to show/hide. If null, this GameObject is used.")]
        [SerializeField] private GameObject keyIconRoot;

        private bool _subscribed;
        Coroutine _waitCo;

        private void Awake()
        {
            if (keyIconRoot == null)
                keyIconRoot = gameObject;

            if (keyIconRoot != null)
                keyIconRoot.SetActive(false);
        }

        private void OnEnable()
        {
            TrySubscribeOrWait();
        }

        private void OnDisable()
        {
            if (_waitCo != null)
            {
                StopCoroutine(_waitCo);
                _waitCo = null;
            }

            if (_subscribed)
            {
                var mgr = DungeonMapManager.Instance;
                if (mgr != null)
                    mgr.KeyStateChanged -= OnKeyStateChanged;

                _subscribed = false;
            }
        }

        void TrySubscribeOrWait()
        {
            if (_subscribed) return;

            var mgr = DungeonMapManager.Instance;
            if (mgr != null)
            {
                mgr.KeyStateChanged += OnKeyStateChanged;
                _subscribed = true;
                OnKeyStateChanged(mgr.PlayerHasKey);
            }
            else
            {
                if (_waitCo == null)
                    _waitCo = StartCoroutine(WaitForManager());
            }
        }

        IEnumerator WaitForManager()
        {
            while (!_subscribed)
            {
                var mgr = DungeonMapManager.Instance;
                if (mgr != null)
                {
                    mgr.KeyStateChanged += OnKeyStateChanged;
                    _subscribed = true;
                    OnKeyStateChanged(mgr.PlayerHasKey);
                    _waitCo = null;
                    yield break;
                }
                yield return null;
            }
        }

        private void OnKeyStateChanged(bool hasKey)
        {
            if (keyIconRoot != null)
                keyIconRoot.SetActive(hasKey);
        }
    }
}
