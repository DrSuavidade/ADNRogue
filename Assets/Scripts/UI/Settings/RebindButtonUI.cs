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
        private const string PlayerPrefsKey = "Keybinds";

        private void Awake()
        {
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

                    var pi = FindObjectOfType<PlayerInput>();
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
