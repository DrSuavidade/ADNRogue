using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Geneforge.UI
{
public class VideoResolutionUI : MonoBehaviour
{
    [Header("Ligação de UI")]
    public TMP_Dropdown DD_Resolution;    // Content/Row_Resolution/DD_Resolution
    public TMP_Dropdown DD_Fullscreen;    // Content/Row_Fullscreen/DD_Fullscreen

    [Header("V-Sync e FPS")]
    public TMP_Dropdown DD_VSync;         // Content/Row_VSync/DD_VSync
    public TMP_Dropdown DD_FPS;           // Content/Row_FPS/DD_FPS  (opções: 60, 120)

    private readonly List<(int w, int h)> resList = new();
    private int currentResIndex = 0;

    // PlayerPrefs keys
    const string KEY_RES_W = "vid_res_w";
    const string KEY_RES_H = "vid_res_h";
    const string KEY_MODE = "vid_mode";
    const string KEY_VSYNC = "vid_vsync"; // 0,1,2
    const string KEY_FPS = "vid_fps";   // 60 ou 120

    void Awake()
    {
        BuildResolutions();
        BuildFullscreenModes();
        BuildVSyncOptions();
        BuildFPSOptions();

        RestoreSavedOrCurrent();
        WireEvents();
    }

    // ---------- Build UI options ----------
    void BuildResolutions()
    {
        DD_Resolution.ClearOptions();
        resList.Clear();

        // Lista fixa (ordem do maior para o menor)
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

    void BuildFPSOptions()
    {
        if (!DD_FPS) return;
        DD_FPS.ClearOptions();
        DD_FPS.AddOptions(new List<string> { "60 FPS", "120 FPS" });
    }

    // ---------- Restore & wire ----------
    void RestoreSavedOrCurrent()
    {
        // Fullscreen mode
        int modeIdx = PlayerPrefs.GetInt(KEY_MODE, ModeToIndex(Screen.fullScreenMode));
        modeIdx = Mathf.Clamp(modeIdx, 0, DD_Fullscreen.options.Count - 1);
        DD_Fullscreen.value = modeIdx;

        // Resolution (usa guardada, senão tenta a atual)
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

        // FPS (60 por defeito)
        int fpsSaved = PlayerPrefs.GetInt(KEY_FPS, 60);
        int fpsIdx = (fpsSaved >= 120) ? 1 : 0;
        if (DD_FPS)
        {
            DD_FPS.value = fpsIdx;
        }

        // Aplicar tudo
        ApplyMode(modeIdx);
        ApplyResolution(currentResIndex, false);
        ApplyFpsRespectingVSync(); // respeita estado atual do VSync

        // Refresh UI
        DD_Fullscreen.RefreshShownValue();
        DD_Resolution.RefreshShownValue();
        if (DD_VSync) DD_VSync.RefreshShownValue();
        if (DD_FPS) DD_FPS.RefreshShownValue();
    }

    void WireEvents()
    {
        DD_Resolution.onValueChanged.AddListener(OnResolutionChanged);
        DD_Fullscreen.onValueChanged.AddListener(OnModeChanged);

        if (DD_VSync) DD_VSync.onValueChanged.AddListener(OnVSyncChanged);
        if (DD_FPS) DD_FPS.onValueChanged.AddListener(OnFPSDropdownChanged);
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
        QualitySettings.vSyncCount = index;  // 0/1/2
        ApplyFpsRespectingVSync();           // se VSync ON, ignora targetFrameRate
        SavePrefs();
    }

    void OnFPSDropdownChanged(int index)
    {
        // 0 → 60, 1 → 120
        int fps = index == 1 ? 120 : 60;
        PlayerPrefs.SetInt(KEY_FPS, fps);
        ApplyFpsRespectingVSync();
        SavePrefs();
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
        // Se VSync estiver ON (1 ou 2), o targetFrameRate é ignorado → coloca -1
        int vsync = DD_VSync ? DD_VSync.value : QualitySettings.vSyncCount;
        if (vsync >= 1)
        {
            Application.targetFrameRate = -1; // deixa o VSync mandar
#if UNITY_EDITOR
            Debug.Log("[Video] VSync ativo → targetFrameRate ignorado.");
#endif
        }
        else
        {
            // VSync OFF → aplica o FPS escolhido
            int fps = PlayerPrefs.GetInt(KEY_FPS, 60);
            Application.targetFrameRate = fps;
#if UNITY_EDITOR
            Debug.Log($"[Video] VSync OFF → targetFrameRate = {fps}");
#endif
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
        return m switch
        {
            FullScreenMode.Windowed => 0,
            FullScreenMode.FullScreenWindow => 1,
            FullScreenMode.MaximizedWindow => 0,
            _ => 0
        };
    }

    FullScreenMode IndexToMode(int i)
    {
        return i switch
        {
            0 => FullScreenMode.Windowed,
            1 => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.FullScreenWindow
        };
    }

    void SavePrefs()
    {
        var (w, h) = resList[currentResIndex];
        PlayerPrefs.SetInt(KEY_RES_W, w);
        PlayerPrefs.SetInt(KEY_RES_H, h);
        PlayerPrefs.SetInt(KEY_MODE, DD_Fullscreen.value);

        if (DD_VSync) PlayerPrefs.SetInt(KEY_VSYNC, DD_VSync.value);
        if (DD_FPS) PlayerPrefs.SetInt(KEY_FPS, DD_FPS.value == 1 ? 120 : 60);

        PlayerPrefs.Save();
    }
}
}