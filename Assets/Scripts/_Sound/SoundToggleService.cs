using System;
using UnityEngine;
/// <summary>
/// Global on/off sound switch.
/// - Persists via PlayerPrefs.
/// - Mutes/unmutes all audio (SFX + Music).
/// - Scene independent.
/// </summary>
public static class SoundToggleService
{
    private const string PREF_KEY = "SoundEnabled";
    private static bool isInitialized;
    private static bool isSoundEnabled = true;

    public static event Action<bool> OnSoundStateChanged;

    /// <summary>
    /// Current sound enabled state.
    /// </summary>
    public static bool IsSoundEnabled
    {
        get
        {
            EnsureInitialized();
            return isSoundEnabled;
        }
        private set
        {
            if (isSoundEnabled == value) return;
            isSoundEnabled = value;

            ApplyToUnityAudio(isSoundEnabled);
            Save(isSoundEnabled);

            OnSoundStateChanged?.Invoke(isSoundEnabled);
        }
    }

    /// <summary>
    /// Call once early (optional). Automatically called on first access anyway.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        isSoundEnabled = PlayerPrefs.GetInt(PREF_KEY, 1) == 1;
        ApplyToUnityAudio(isSoundEnabled);
    }

    /// <summary>
    /// Toggle sound on/off.
    /// </summary>
    public static void Toggle()
    {
        SetEnabled(!IsSoundEnabled);
    }

    /// <summary>
    /// Explicitly set sound enabled state.
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        EnsureInitialized();
        IsSoundEnabled = enabled;
    }

    private static void ApplyToUnityAudio(bool enabled)
    {
        // Simple global mute for everything:
        AudioListener.pause = !enabled;

        // Optional extra safety:
        AudioListener.volume = enabled ? 1f : 0f;
    }

    private static void Save(bool enabled)
    {
        PlayerPrefs.SetInt(PREF_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
