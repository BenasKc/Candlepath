using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public enum BindAction { CallWave, AutoWaves, Rebuild, Clear, NextBurnt, Pause }

public static class GameSettings
{
    public static event Action Changed;

    const string P = "settings.";

    // Volumes 0..1. Master scales everything; MusicVolume is what MapMusic uses (the effective music level is Music * Master).
    public static float Master { get => Get("master", 1f); set { Set("master", value); AudioListener.volume = value; } }
    public static float Music { get => Get("music", 1f); set => Set("music", value); }
    public static float Sfx { get => Get("sfx", 1f); set => Set("sfx", value); }
    public static float Ambient { get => Get("ambient", 1f); set => Set("ambient", value); }

    // 0 = the authored look, 1 = about 1.5 stops brighter (post exposure on the scene's URP volumes)
    public static float Brightness { get => Get("brightness", 0f); set { Set("brightness", value); ApplyBrightness(); } }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(P + "fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        set { PlayerPrefs.SetInt(P + "fullscreen", value ? 1 : 0); ApplyDisplay(); Changed?.Invoke(); }
    }

    public static bool VSync
    {
        get => PlayerPrefs.GetInt(P + "vsync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        set { PlayerPrefs.SetInt(P + "vsync", value ? 1 : 0); ApplyDisplay(); Changed?.Invoke(); }
    }

    // Distinct width x height pairs the display supports, ascending
    public static List<Vector2Int> Resolutions()
    {
        var list = new List<Vector2Int>();
        foreach (var r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        return list;
    }

    public static Vector2Int Resolution
    {
        get => new Vector2Int(PlayerPrefs.GetInt(P + "resW", Screen.width), PlayerPrefs.GetInt(P + "resH", Screen.height));
        set
        {
            PlayerPrefs.SetInt(P + "resW", value.x);
            PlayerPrefs.SetInt(P + "resH", value.y);
            ApplyDisplay();
            Changed?.Invoke();
        }
    }

    // ----- Keys -----

    static readonly Dictionary<BindAction, Key> Defaults = new Dictionary<BindAction, Key>
    {
        { BindAction.CallWave, Key.Space },
        { BindAction.AutoWaves, Key.C },
        { BindAction.Rebuild, Key.R },
        { BindAction.Clear, Key.X },
        { BindAction.NextBurnt, Key.B },
        { BindAction.Pause, Key.P },
    };

    // Keys the game reads directly (TowerBuilder: candles 1-9 on both rows, Shift to keep placing, Delete;
    // Esc everywhere). Binding an action to one of these would fire both, so they can't be bound.
    public static bool IsReserved(Key key) =>
        key == Key.None || key == Key.Escape || key == Key.Delete ||
        key == Key.LeftShift || key == Key.RightShift ||
        (key >= Key.Digit1 && key <= Key.Digit9) ||
        (key >= Key.Numpad1 && key <= Key.Numpad9);

    public static Key GetKey(BindAction action)
    {
        var key = (Key)PlayerPrefs.GetInt(P + "key." + action, (int)Defaults[action]);
        // A binding saved before keys were reserved falls back to the default
        return IsReserved(key) ? Defaults[action] : key;
    }

    public static void SetKey(BindAction action, Key key)
    {
        if (IsReserved(key)) return;
        // Swap with whichever action already uses this key so no two actions share one
        foreach (BindAction other in Enum.GetValues(typeof(BindAction)))
            if (other != action && GetKey(other) == key)
                PlayerPrefs.SetInt(P + "key." + other, (int)GetKey(action));
        PlayerPrefs.SetInt(P + "key." + action, (int)key);
        Changed?.Invoke();
    }

    public static void ResetKeys()
    {
        foreach (BindAction a in Enum.GetValues(typeof(BindAction))) PlayerPrefs.DeleteKey(P + "key." + a);
        Changed?.Invoke();
    }

    public static bool Pressed(BindAction action)
    {
        var kb = Keyboard.current;
        return kb != null && !SettingsController.IsOpen && kb[GetKey(action)].wasPressedThisFrame;
    }

    public static string KeyName(Key key)
    {
        string s = key.ToString();
        if (s.StartsWith("Digit")) return s.Substring(5);
        return s;
    }

    public static string ActionName(BindAction a) => a switch
    {
        BindAction.CallWave => "Start wave",
        BindAction.AutoWaves => "Auto waves",
        BindAction.Rebuild => "Rebuild burnt-out candle",
        BindAction.Clear => "Clear wax puddle",
        BindAction.NextBurnt => "Next burnt-out candle",
        _ => "Pause (also Esc)",
    };

    // ----- Applying -----

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        AudioListener.volume = Master;
        if (PlayerPrefs.HasKey(P + "resW") || PlayerPrefs.HasKey(P + "fullscreen")) ApplyDisplay();
        else if (PlayerPrefs.HasKey(P + "vsync")) QualitySettings.vSyncCount = VSync ? 1 : 0;
        ApplyBrightness();
        SceneManager.sceneLoaded += (_, __) => ApplyBrightness();
    }

    static void ApplyDisplay()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        var r = Resolution;
        Screen.SetResolution(r.x, r.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    public static void ApplyBrightness()
    {
        float exposure = Brightness * 1.5f;
        foreach (var volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var profile = volume.profile; // instance, so the shared asset isn't edited
            if (profile == null) continue;
            if (!profile.TryGet(out ColorAdjustments adjust)) adjust = profile.Add<ColorAdjustments>();
            adjust.postExposure.Override(exposure);
        }
        Changed?.Invoke();
    }

    public static void NotifyChanged() => Changed?.Invoke();

    static float Get(string key, float def) => PlayerPrefs.GetFloat(P + key, def);

    static void Set(string key, float value)
    {
        PlayerPrefs.SetFloat(P + key, value);
        Changed?.Invoke();
    }
}
