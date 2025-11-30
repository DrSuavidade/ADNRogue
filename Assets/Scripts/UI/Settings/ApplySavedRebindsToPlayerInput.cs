using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI.Settings
{
    [DefaultExecutionOrder(-100)]
    public class ApplySavedRebindsToPlayerInput : MonoBehaviour
    {
        public PlayerInput playerInput;

        void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            if (playerInput == null) return;

            var json = PlayerPrefs.GetString(RebindButtonUI.PlayerPrefsKey, "");
            if (!string.IsNullOrEmpty(json))
                playerInput.actions.LoadBindingOverridesFromJson(json);
        }
    }
}
