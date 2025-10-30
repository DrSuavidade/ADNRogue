using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

namespace Geneforge.UI
{
    public class AudioSettingsUI : MonoBehaviour
    {
        public AudioMixer Mixer;

        public Slider SL_Master;   public TMP_Text TX_Master;
        public Slider SL_Music;    public TMP_Text TX_Music;
        public Slider SL_Game;     public TMP_Text TX_Game;
        public Slider SL_Ambience; public TMP_Text TX_Ambience;
        public Slider SL_UI;       public TMP_Text TX_UI;

        const string K_MASTER="aud_master", K_MUSIC="aud_music", K_GAME="aud_game",
                     K_AMBI="aud_ambience", K_UI="aud_ui";

        void Awake()
        {
            Setup(SL_Master,   TX_Master,   "MasterVolume",   K_MASTER);
            Setup(SL_Music,    TX_Music,    "MusicVolume",    K_MUSIC);
            Setup(SL_Game,     TX_Game,     "GameVolume",     K_GAME);
            Setup(SL_Ambience, TX_Ambience, "AmbienceVolume", K_AMBI);
            Setup(SL_UI,       TX_UI,       "UIVolume",       K_UI);
        }

        void Setup(Slider s, TMP_Text t, string param, string key)
        {
            if (!s) return;

            if (s.minValue <= 0f) s.minValue = 0.0001f;
            if (s.maxValue < 1f)  s.maxValue = 1f;

            float v = PlayerPrefs.GetFloat(key, 1f);
            v = Mathf.Clamp(v, s.minValue, s.maxValue);
            s.SetValueWithoutNotify(v);
            ApplyLinear(param, v);
            if (t) t.text = v.ToString("0.00");

            s.onValueChanged.AddListener(val => {
                float clamped = Mathf.Clamp(val, s.minValue, s.maxValue);
                ApplyLinear(param, clamped);
                if (t) t.text = clamped.ToString("0.00");
                PlayerPrefs.SetFloat(key, clamped);
                PlayerPrefs.Save();
            });
        }

        void ApplyLinear(string param, float linear)
        {
            float dB = Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f;
            if (Mixer) Mixer.SetFloat(param, dB);
        }
    }
}
