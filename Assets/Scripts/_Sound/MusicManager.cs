using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// MusicManager
/// Persistent playlist-based music system with logarithmic crossfade.
/// - Holds two playlists: Menu and Gameplay.
/// - Swaps playlists by mode without restarting if already active.
/// - Uses 2 AudioSources (current/previous) for smooth crossfade.
/// - Auto-plays next track when current ends.
/// </summary>
public class MusicManager : PersistentSingleton<MusicManager>
{
    #region Types

    private enum MusicMode
    {
        None,
        Menu,
        Gameplay
    }

    #endregion

    #region Inspector Fields

    [Header("Mixer")]
    [SerializeField, Tooltip("Output group for music.")]
    private AudioMixerGroup musicMixerGroup;

    [Header("Crossfade")]
    [SerializeField, Range(0.1f, 10f), Tooltip("Seconds to crossfade between tracks.")]
    private float crossFadeTime = 2f;

    [Header("Playlists")]
    [SerializeField, Tooltip("Tracks used in the main menu.")]
    private List<AudioClip> menuPlaylist = new();

    [SerializeField, Tooltip("Tracks used during gameplay.")]
    private List<AudioClip> gameplayPlaylist = new();

    #endregion

    #region Private Fields

    private readonly Queue<AudioClip> playlist = new();

    private AudioSource current;
    private AudioSource previous;

    private float fading; // 0 = no fade, >0 = active fade timer
    private MusicMode currentMode = MusicMode.None;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();
        // Ensure we have at least one AudioSource ready.
        current = gameObject.GetOrAdd<AudioSource>();
        ConfigureSource(current);
    }

    private void Update()
    {
        HandleCrossFade();

        // If current finished, play next.
        if (current != null && !current.isPlaying && fading <= 0f && playlist.Count > 0)
        {
            PlayNextTrack();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Switch to the Menu playlist (if not already active).
    /// </summary>
    public void PlayMenuMusic()
    {
        if (currentMode == MusicMode.Menu) return;
        currentMode = MusicMode.Menu;
        SetPlaylist(menuPlaylist);
    }

    /// <summary>
    /// Switch to the Gameplay playlist (if not already active).
    /// </summary>
    public void PlayGameplayMusic()
    {
        if (currentMode == MusicMode.Gameplay) return;
        currentMode = MusicMode.Gameplay;
        SetPlaylist(gameplayPlaylist);
    }

    /// <summary>
    /// Clears current queue and enqueues the given clips, then starts immediately.
    /// </summary>
    public void SetPlaylist(List<AudioClip> clips, bool startImmediately = true)
    {
        if (clips == null || clips.Count == 0)
        {
            Debug.LogWarning("[MusicManager] SetPlaylist called with empty list.");
            return;
        }

        ClearPlaylistInternal();

        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] != null)
                playlist.Enqueue(clips[i]);
        }

        if (startImmediately)
            PlayNextTrack();
    }

    /// <summary>
    /// Adds a single clip to the end of the current playlist.
    /// If nothing is playing, it starts immediately.
    /// </summary>
    public void AddToPlaylist(AudioClip clip)
    {
        if (clip == null) return;

        playlist.Enqueue(clip);

        if (current != null && !current.isPlaying && fading <= 0f)
        {
            PlayNextTrack();
        }
    }

    /// <summary>
    /// Clears playlist and stops audio (with no fade).
    /// </summary>
    public void StopMusic()
    {
        ClearPlaylistInternal();

        if (current != null)
            current.Stop();

        if (previous != null)
        {
            Destroy(previous);
            previous = null;
        }

        fading = 0f;
        currentMode = MusicMode.None;
    }

    #endregion

    #region Core Playback

    private void PlayNextTrack()
    {
        if (playlist.Count == 0) return;

        AudioClip next = playlist.Dequeue();
        if (next == null) return;

        Play(next);

        // Optional: loop playlist forever by re-enqueueing.
        playlist.Enqueue(next);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null) return;

        // If current already playing this exact track, do nothing.
        if (current != null && current.isPlaying && current.clip == clip)
            return;

        // Cleanup any leftover previous source
        if (previous != null)
        {
            Destroy(previous);
            previous = null;
        }

        // Move current → previous
        if (current != null)
        {
            previous = current;
        }

        // Create a new current source
        current = gameObject.GetOrAdd<AudioSource>();
        ConfigureSource(current);

        current.clip = clip;
        current.volume = 0f; // fade-in from zero
        current.Play();

        fading = Mathf.Max(0.01f, crossFadeTime);
    }

    private void ConfigureSource(AudioSource source)
    {
        if (source == null) return;

        source.loop = false; // playlist handles looping
        source.playOnAwake = false;
        source.outputAudioMixerGroup = musicMixerGroup;

        // keep music consistent / clean
        source.bypassEffects = true;
        source.bypassListenerEffects = true;
        source.dopplerLevel = 0f;
        source.spatialBlend = 0f; // 2D music
    }

    #endregion

    #region Crossfade

    private void HandleCrossFade()
    {
        if (fading <= 0f) return;

        fading -= Time.unscaledDeltaTime;
        float t = 1f - Mathf.Clamp01(fading / crossFadeTime);

        // logarithmic fraction for nicer fade curve
        float logT = t.ToLogarithmicFraction();

        if (previous != null)
            previous.volume = 1f - logT;

        if (current != null)
            current.volume = logT;

        if (fading <= 0f)
        {
            // Fade done; kill previous
            if (previous != null)
            {
                previous.Stop();
                Destroy(previous);
                previous = null;
            }

            if (current != null)
                current.volume = 1f;

            fading = 0f;
        }
    }

    #endregion

    #region Helpers

    private void ClearPlaylistInternal()
    {
        playlist.Clear();
    }

    #endregion
}
