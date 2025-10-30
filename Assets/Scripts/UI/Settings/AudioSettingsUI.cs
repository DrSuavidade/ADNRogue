using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

namespace Geneforge.UI
{
public class AudioSettingsUI : MonoBehaviour
{
    [Header("Mixer Principal")]
    public AudioMixer Mixer;

    [Header("Master Volume")]
    public Slider SL_Master;
    public TMP_Text TX_Master;

    [Header("Music Volume")]
    public Slider SL_Music;
    public TMP_Text TX_Music;

    [Header("Game Sounds")]
    public Slider SL_Game;
    public TMP_Text TX_Game;

    [Header("Ambience Volume")]
    public Slider SL_Ambience;
    public TMP_Text TX_Ambience;

    [Header("UI Volume")]
    public Slider SL_UI;
    public TMP_Text TX_UI;

    private void Start()
    {
        // ligar sliders aos métodos
        SL_Master.onValueChanged.AddListener(v => SetVolume("MasterVolume", v, TX_Master));
        SL_Music.onValueChanged.AddListener(v => SetVolume("MusicVolume", v, TX_Music));
        SL_Game.onValueChanged.AddListener(v => SetVolume("GameVolume", v, TX_Game));
        SL_Ambience.onValueChanged.AddListener(v => SetVolume("AmbienceVolume", v, TX_Ambience));
        SL_UI.onValueChanged.AddListener(v => SetVolume("UIVolume", v, TX_UI));
    }

    private void SetVolume(string parameter, float value, TMP_Text txt)
    {
        // converte 0–1 → -80dB a 0dB
        Mixer.SetFloat(parameter, Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1)) * 20);
        if (txt != null) txt.text = value.ToString("0.00");
    }
}
}