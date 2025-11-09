using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PauseableAudioSource : MonoBehaviour, IPausable
{
    private AudioSource audioSource;
    private bool wasPlaying;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        PauseManager.Instance?.Register(this);
        // Sync state if we spawned while paused
        if (PauseManager.Instance) OnStopGameplayIfPaused();
    }

    private void OnDisable()
    {
        PauseManager.Instance?.Unregister(this);
    }

    public void OnStopGameplay()
    {
        if (!audioSource) return;
        wasPlaying = audioSource.isPlaying;
        if (wasPlaying) audioSource.Pause();
    }

    public void OnResumeGameplay()
    {
        if (!audioSource) return;
        if (wasPlaying) audioSource.UnPause();
    }

    private void OnStopGameplayIfPaused()
    {
        if (PauseManager.Instance.IsGameplayStopped) OnStopGameplay();
    }
}
