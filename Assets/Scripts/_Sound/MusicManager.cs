using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// MusicManager
/// Persistent playlist-based music system with logarithmic crossfade.
/// - Holds two playlists: Menu and Gameplay.
/// - Swaps playlists by mode without restarting if already active.
/// - Uses 2 AudioSources (A/B) for smooth crossfade.
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

    // Two fixed sources
    private AudioSource sourceA;
    private AudioSource sourceB;

    // Current active music source and fading-out source
    private AudioSource activeSource;
    private AudioSource fadingOutSource;

    private float fadingTimer; // >0 while a crossfade is active
    private MusicMode currentMode = MusicMode.None;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        // Create exactly two AudioSources and keep them forever.
        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();

        ConfigureSource(sourceA);
        ConfigureSource(sourceB);

        sourceA.volume = 0f;
        sourceB.volume = 0f;

        activeSource = null;
        fadingOutSource = null;
        fadingTimer = 0f;
    }

    private void Update()
    {
        HandleCrossFade();

        // When the active track finishes and no fade is happening, advance playlist.
        if (activeSource != null && !activeSource.isPlaying && fadingTimer <= 0f && playlist.Count > 0)
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

        if (activeSource == null || (!activeSource.isPlaying && fadingTimer <= 0f))
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

        if (activeSource != null)
        {
            activeSource.Stop();
            activeSource.clip = null;
            activeSource.volume = 0f;
        }

        if (fadingOutSource != null)
        {
            fadingOutSource.Stop();
            fadingOutSource.clip = null;
            fadingOutSource.volume = 0f;
        }

        fadingTimer = 0f;
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

        // If already playing this clip on the active source, do nothing.
        if (activeSource != null && activeSource.clip == clip && activeSource.isPlaying)
            return;

        // Decide which physical AudioSource will be the new active
        AudioSource newSource = (activeSource == sourceA) ? sourceB : sourceA;

        newSource.clip = clip;

        // First track or no crossfade configured
        if (activeSource == null || crossFadeTime <= 0f)
        {
            // Hard switch: stop old, play new at full volume.
            if (activeSource != null)
            {
                activeSource.Stop();
                activeSource.volume = 0f;
            }

            newSource.volume = 1f;
            newSource.Play();

            activeSource = newSource;
            fadingOutSource = null;
            fadingTimer = 0f;
        }
        else
        {
            // Crossfade: new starts at 0, old fades out.
            newSource.volume = 0f;
            newSource.Play();

            fadingOutSource = activeSource;
            activeSource = newSource;
            fadingTimer = crossFadeTime;
        }
    }

    private void ConfigureSource(AudioSource source)
    {
        if (source == null) return;

        source.playOnAwake = false;
        source.loop = false; // playlist handles looping
        source.outputAudioMixerGroup = musicMixerGroup;

        // Keep music clean and 2D
        source.bypassEffects = true;
        source.bypassListenerEffects = true;
        source.dopplerLevel = 0f;
        source.spatialBlend = 0f;
    }

    #endregion

    #region Crossfade

    private void HandleCrossFade()
    {
        if (fadingTimer <= 0f || activeSource == null || fadingOutSource == null)
            return;

        fadingTimer -= Time.unscaledDeltaTime;
        float t = 1f - Mathf.Clamp01(fadingTimer / crossFadeTime);

        // nicer curve using your ToLogarithmicFraction() extension
        float logT = t.ToLogarithmicFraction();

        fadingOutSource.volume = 1f - logT;
        activeSource.volume = logT;

        if (fadingTimer <= 0f)
        {
            // Fade completed: stop and clear the old source
            fadingOutSource.Stop();
            fadingOutSource.volume = 0f;
            fadingOutSource.clip = null;
            fadingOutSource = null;

            activeSource.volume = 1f;
            fadingTimer = 0f;
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
