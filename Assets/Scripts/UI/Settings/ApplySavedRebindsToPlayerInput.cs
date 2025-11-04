using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class ApplySavedRebindsToPlayerInput : MonoBehaviour
{
    public PlayerInput playerInput;
    const string PlayerPrefsKey = "Keybinds";

    void Awake()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerInput == null) return;

        var json = PlayerPrefs.GetString(PlayerPrefsKey, "");
        if (!string.IsNullOrEmpty(json))
            playerInput.actions.LoadBindingOverridesFromJson(json);
    }
}
