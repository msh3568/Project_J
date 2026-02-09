using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class DisplaySettings
{
    public enum WindowMode
    {
        Fullscreen = 0,
        Windowed = 1,
        Borderless = 2
    }

    private const string PrefWidth = "DisplayWidth";
    private const string PrefHeight = "DisplayHeight";
    private const string PrefWindowMode = "DisplayWindowMode";
    private const float ApplyCooldownSeconds = 0.3f;
    public static bool DisableApplySavedSettings = false;

    private static readonly Vector2Int DefaultResolution = new Vector2Int(1920, 1080);
    private static List<Vector2Int> cachedResolutions;
    private static float lastApplyTime = -999f;
    private static int lastAppliedWidth = -1;
    private static int lastAppliedHeight = -1;
    private static WindowMode lastAppliedMode = (WindowMode)(-1);

    public static IReadOnlyList<Vector2Int> GetSupportedResolutions()
    {
        if (cachedResolutions != null)
        {
            return cachedResolutions;
        }

        cachedResolutions = new List<Vector2Int>();
        foreach (var res in Screen.resolutions)
        {
            var size = new Vector2Int(res.width, res.height);
            if (!cachedResolutions.Contains(size))
            {
                cachedResolutions.Add(size);
            }
        }

        if (cachedResolutions.Count == 0)
        {
            cachedResolutions.Add(DefaultResolution);
            cachedResolutions.Add(new Vector2Int(1600, 900));
            cachedResolutions.Add(new Vector2Int(1280, 720));
        }

        return cachedResolutions;
    }

    public static WindowMode LoadWindowMode()
    {
        return (WindowMode)PlayerPrefs.GetInt(PrefWindowMode, (int)WindowMode.Fullscreen);
    }

    public static Vector2Int LoadResolution()
    {
        int width = PlayerPrefs.GetInt(PrefWidth, DefaultResolution.x);
        int height = PlayerPrefs.GetInt(PrefHeight, DefaultResolution.y);
        return new Vector2Int(width, height);
    }

    public static void ApplySavedSettings()
    {
        if (DisableApplySavedSettings)
            return;
        var mode = LoadWindowMode();
        var resolution = LoadResolution();
        ApplySettings(resolution.x, resolution.y, mode, false);
    }

    public static void ApplyResolution(int width, int height)
    {
        var mode = LoadWindowMode();
        if (mode != WindowMode.Windowed)
        {
            Debug.Log("DisplaySettings.ApplyResolution: forcing Windowed to apply resolution.");
            ApplySettings(width, height, WindowMode.Windowed, true);
            return;
        }

        ApplySettings(width, height, mode, true);
    }

    public static void ApplyWindowMode(WindowMode mode)
    {
        var resolution = LoadResolution();
        ApplySettings(resolution.x, resolution.y, mode, true);
    }

    public static void ApplySettings(int width, int height, WindowMode mode, bool save)
    {
        float now = Time.realtimeSinceStartup;
        int saveWidth = width;
        int saveHeight = height;
        WindowMode saveMode = mode;
        var fullScreenMode = GetUnityFullScreenMode(mode);
        if (mode == WindowMode.Borderless)
        {
            Debug.Log("DisplaySettings.ApplySettings: Borderless uses desktop resolution; size change may be ignored.");
        }

        if (mode == WindowMode.Fullscreen && !IsResolutionSupported(width, height))
        {
            Debug.LogWarning($"DisplaySettings.ApplySettings: {width}x{height} not supported for Exclusive Fullscreen. Using current resolution.");
            width = Screen.currentResolution.width;
            height = Screen.currentResolution.height;
        }

        if (mode == WindowMode.Borderless)
        {
            width = Screen.currentResolution.width;
            height = Screen.currentResolution.height;
        }

        if (lastAppliedMode == mode &&
            lastAppliedWidth == width &&
            lastAppliedHeight == height &&
            now - lastApplyTime < ApplyCooldownSeconds)
        {
            Debug.Log("DisplaySettings.ApplySettings: skipped duplicate apply within cooldown.");
            return;
        }

        if (Screen.width == width && Screen.height == height && Screen.fullScreenMode == fullScreenMode)
        {
            lastAppliedWidth = width;
            lastAppliedHeight = height;
            lastAppliedMode = mode;
            lastApplyTime = now;
            return;
        }

        Screen.fullScreen = mode != WindowMode.Windowed;
        Screen.fullScreenMode = fullScreenMode;
        Screen.SetResolution(width, height, fullScreenMode);
        if (Screen.fullScreenMode != fullScreenMode)
        {
            if (mode == WindowMode.Fullscreen)
            {
                Debug.LogWarning("DisplaySettings.ApplySettings: Exclusive Fullscreen rejected; falling back to Borderless.");
                fullScreenMode = FullScreenMode.FullScreenWindow;
                width = Screen.currentResolution.width;
                height = Screen.currentResolution.height;
                Screen.fullScreen = true;
                Screen.fullScreenMode = fullScreenMode;
                Screen.SetResolution(width, height, fullScreenMode);
                saveMode = WindowMode.Borderless;
            }
            else if (mode == WindowMode.Windowed)
            {
                Debug.LogWarning("DisplaySettings.ApplySettings: Windowed request rejected; retrying Windowed.");
                fullScreenMode = FullScreenMode.Windowed;
                Screen.fullScreen = false;
                Screen.fullScreenMode = fullScreenMode;
                Screen.SetResolution(width, height, fullScreenMode);
            }
        }

        Debug.Log($"DisplaySettings.ApplySettings: requested {saveWidth}x{saveHeight}, mode={mode}, unityMode={GetUnityFullScreenMode(mode)}");
        Debug.Log($"DisplaySettings.ApplySettings: actual {Screen.width}x{Screen.height}, unityMode={Screen.fullScreenMode}");

        lastAppliedWidth = Screen.width;
        lastAppliedHeight = Screen.height;
        lastAppliedMode = mode;
        lastApplyTime = now;

        if (save)
        {
            PlayerPrefs.SetInt(PrefWidth, saveWidth);
            PlayerPrefs.SetInt(PrefHeight, saveHeight);
            PlayerPrefs.SetInt(PrefWindowMode, (int)saveMode);
        }
    }

    public static void ConfigureAllCanvasScalers()
    {
        var scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var scaler in scalers)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private static FullScreenMode GetUnityFullScreenMode(WindowMode mode)
    {
        switch (mode)
        {
            case WindowMode.Windowed:
                return FullScreenMode.Windowed;
            case WindowMode.Borderless:
                return FullScreenMode.FullScreenWindow;
            default:
                return FullScreenMode.ExclusiveFullScreen;
        }
    }

    private static bool IsResolutionSupported(int width, int height)
    {
        foreach (var res in Screen.resolutions)
        {
            if (res.width == width && res.height == height)
            {
                return true;
            }
        }

        return false;
    }
}
