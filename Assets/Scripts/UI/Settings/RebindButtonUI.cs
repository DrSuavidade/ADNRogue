namespace Game.UI.Settings
{
    using UnityEngine;
    using TMPro;
    using UnityEngine.UI;
    using UnityEngine.InputSystem;

    public class RebindButtonUI : MonoBehaviour
    {
        public InputActionReference actionReference;
        public int bindingIndex = 0;
        public TMP_Text bindingLabel;
        public Button rebindButton;
        private InputAction action;
        public const string PlayerPrefsKey = "Keybinds";

        private void Awake()
        {
            if (actionReference == null || actionReference.action == null)
            {
                Debug.LogError($"[RebindButtonUI] Missing actionReference on {name}. Disabling.", this);
                enabled = false;
                return;
            }

            if (bindingLabel == null || rebindButton == null)
            {
                Debug.LogError($"[RebindButtonUI] Missing UI references (bindingLabel / rebindButton) on {name}. Disabling.", this);
                enabled = false;
                return;
            }

            action = actionReference.action;
            LoadRebindsIntoAsset();
            UpdateUI();
            rebindButton.onClick.AddListener(StartRebind);
        }

        private void StartRebind()
        {
            rebindButton.interactable = false;
            action.Disable();

            action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    op.Dispose();
                    action.Enable();
                    SaveRebindsFromAsset();

                    var pi = FindAnyObjectByType<PlayerInput>();
                    if (pi != null)
                    {
                        var json = PlayerPrefs.GetString(PlayerPrefsKey, "");
                        if (!string.IsNullOrEmpty(json))
                            pi.actions.LoadBindingOverridesFromJson(json);
                    }

                    UpdateUI();
                    rebindButton.interactable = true;
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    action.Enable();
                    rebindButton.interactable = true;
                })
                .Start();
        }

        private void UpdateUI()
        {
            if (bindingLabel == null || action == null) return;
            bindingLabel.text = action.GetBindingDisplayString(bindingIndex);
        }

        private void SaveRebindsFromAsset()
        {
            string json = action.actionMap.asset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        private void LoadRebindsIntoAsset()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey)) return;
            string json = PlayerPrefs.GetString(PlayerPrefsKey);
            action.actionMap.asset.LoadBindingOverridesFromJson(json);
        }
    }
}
