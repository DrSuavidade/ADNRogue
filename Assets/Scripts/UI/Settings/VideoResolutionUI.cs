using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Geneforge.UI
{
    public class VideoResolutionUI : MonoBehaviour
    {
        [Header("Ligação de UI")]
        public TMP_Dropdown DD_Resolution;
        public TMP_Dropdown DD_Fullscreen;

        [Header("V-Sync e FPS")]
        public TMP_Dropdown DD_VSync;
        public Slider SL_FPS;
        public TMP_Text TX_FPSVal;

        private readonly List<(int w, int h)> resList = new();
        private int currentResIndex = 0;

        const string KEY_RES_W = "vid_res_w";
        const string KEY_RES_H = "vid_res_h";
        const string KEY_MODE = "vid_mode";
        const string KEY_VSYNC = "vid_vsync";
        const string KEY_FPS = "vid_fps";

        void Awake()
        {
            BuildResolutions();
            BuildFullscreenModes();
            BuildVSyncOptions();

            RestoreSavedOrCurrent();
            WireEvents();
        }


        // ---------- Build UI options ----------
        void BuildResolutions()
        {
            DD_Resolution.ClearOptions();
            resList.Clear();

            resList.Add((1920, 1080));
            resList.Add((1680, 1050));
            resList.Add((1600, 900));
            resList.Add((1440, 900));
            resList.Add((1366, 768));
            resList.Add((1280, 720));

            var options = new List<string>(resList.Count);
            foreach (var r in resList) options.Add($"{r.w}x{r.h}");
            DD_Resolution.AddOptions(options);
        }

        void BuildFullscreenModes()
        {
            DD_Fullscreen.ClearOptions();
            DD_Fullscreen.AddOptions(new List<string> {
                "Windowed",
                "Fullscreen Window"
            });
        }

        void BuildVSyncOptions()
        {
            if (!DD_VSync) return;
            DD_VSync.ClearOptions();
            DD_VSync.AddOptions(new List<string> {
                "Off",
                "Every VBlank",
                "Every 2 VBlanks"
            });
        }


        // ---------- Restore & wire ----------
        void RestoreSavedOrCurrent()
        {
            // Fullscreen mode
            int modeIdx = PlayerPrefs.GetInt(KEY_MODE, ModeToIndex(Screen.fullScreenMode));
            modeIdx = Mathf.Clamp(modeIdx, 0, DD_Fullscreen.options.Count - 1);
            DD_Fullscreen.value = modeIdx;

            // Resolution
            int savedW = PlayerPrefs.GetInt(KEY_RES_W, Screen.currentResolution.width);
            int savedH = PlayerPrefs.GetInt(KEY_RES_H, Screen.currentResolution.height);
            currentResIndex = FindResIndex(savedW, savedH);
            DD_Resolution.value = currentResIndex;

            // VSync
            int vsync = PlayerPrefs.GetInt(KEY_VSYNC, QualitySettings.vSyncCount);
            vsync = Mathf.Clamp(vsync, 0, 2);
            if (DD_VSync)
            {
                DD_VSync.value = vsync;
                QualitySettings.vSyncCount = vsync;
            }

            // FPS (default 60 → até 120)
            if (SL_FPS)
            {
                SL_FPS.wholeNumbers = true;
                SL_FPS.minValue = 60;
                SL_FPS.maxValue = 120;

                int fpsSaved = PlayerPrefs.GetInt(KEY_FPS, 60);
                fpsSaved = Mathf.Clamp(fpsSaved, (int)SL_FPS.minValue, (int)SL_FPS.maxValue);
                SL_FPS.SetValueWithoutNotify(fpsSaved);
                if (TX_FPSVal) TX_FPSVal.text = fpsSaved.ToString();
            }

            ApplyMode(modeIdx);
            ApplyResolution(currentResIndex, false);
            ApplyFpsRespectingVSync();

            DD_Fullscreen.RefreshShownValue();
            DD_Resolution.RefreshShownValue();
            if (DD_VSync) DD_VSync.RefreshShownValue();
        }

        void WireEvents()
        {
            DD_Resolution.onValueChanged.AddListener(OnResolutionChanged);
            DD_Fullscreen.onValueChanged.AddListener(OnModeChanged);

            if (DD_VSync) DD_VSync.onValueChanged.AddListener(OnVSyncChanged);
            if (SL_FPS) SL_FPS.onValueChanged.AddListener(OnFPSSliderChanged);
        }


        // ---------- UI callbacks ----------
        void OnResolutionChanged(int index)
        {
            currentResIndex = index;
            ApplyResolution(index, true);
            SavePrefs();
        }

        void OnModeChanged(int index)
        {
            ApplyMode(index);
            ApplyResolution(currentResIndex, false);
            SavePrefs();
        }

        void OnVSyncChanged(int index)
        {
            QualitySettings.vSyncCount = index;
            ApplyFpsRespectingVSync();
            SavePrefs();
        }

        void OnFPSSliderChanged(float value)
        {
            int fps = Mathf.RoundToInt(value);
            if (TX_FPSVal) TX_FPSVal.text = fps.ToString();

            if (QualitySettings.vSyncCount == 0)
                Application.targetFrameRate = fps;

            PlayerPrefs.SetInt(KEY_FPS, fps);
        }


        // ---------- Apply ----------
        void ApplyResolution(int idx, bool log)
        {
            idx = Mathf.Clamp(idx, 0, resList.Count - 1);
            var (w, h) = resList[idx];
            var mode = IndexToMode(DD_Fullscreen.value);

            Screen.fullScreenMode = mode;
            Screen.SetResolution(w, h, mode);

#if UNITY_EDITOR
            if (log) Debug.Log($"[Video] Resolução: {w}x{h}, Modo: {mode}");
#endif
        }

        void ApplyMode(int idx)
        {
            var mode = IndexToMode(idx);
            Screen.fullScreenMode = mode;
        }

        void ApplyFpsRespectingVSync()
        {
            int vsync = DD_VSync ? DD_VSync.value : QualitySettings.vSyncCount;
            if (vsync >= 1)
            {
                Application.targetFrameRate = -1;
            }
            else if (SL_FPS)
            {
                int fps = Mathf.RoundToInt(SL_FPS.value);
                Application.targetFrameRate = fps;
            }
        }


        // ---------- Helpers ----------
        int FindResIndex(int w, int h)
        {
            for (int i = 0; i < resList.Count; i++)
                if (resList[i].w == w && resList[i].h == h) return i;
            return 0;
        }

        int ModeToIndex(FullScreenMode m)
        {
            switch (m)
            {
                case FullScreenMode.Windowed: return 0;
                case FullScreenMode.FullScreenWindow: return 1;
                case FullScreenMode.MaximizedWindow: return 0;
                default: return 0;
            }
        }

        FullScreenMode IndexToMode(int i)
        {
            switch (i)
            {
                case 0: return FullScreenMode.Windowed;
                case 1: return FullScreenMode.FullScreenWindow;
                default: return FullScreenMode.FullScreenWindow;
            }
        }

        void SavePrefs()
        {
            var (w, h) = resList[currentResIndex];
            PlayerPrefs.SetInt(KEY_RES_W, w);
            PlayerPrefs.SetInt(KEY_RES_H, h);
            PlayerPrefs.SetInt(KEY_MODE, DD_Fullscreen.value);

            if (DD_VSync) PlayerPrefs.SetInt(KEY_VSYNC, DD_VSync.value);
            if (SL_FPS) PlayerPrefs.SetInt(KEY_FPS, Mathf.RoundToInt(SL_FPS.value));

            PlayerPrefs.Save();
        }

        void OnDisable()
        {
            PlayerPrefs.Save();
        }
    }
}
