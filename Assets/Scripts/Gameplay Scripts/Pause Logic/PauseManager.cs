using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1)]
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }
    public bool IsGameplayStopped { get; private set; }

    private readonly HashSet<IStoppable> pauseListeners = new HashSet<IStoppable>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(IStoppable listener)
    {
        if (listener == null) return;
        pauseListeners.Add(listener);

        // if we’re already paused, immediately push OnStopGameplay()
        if (IsGameplayStopped)
            listener.OnStopGameplay();
    }

    public void Unregister(IStoppable listener)
    {
        if (listener != null) pauseListeners.Remove(listener);
    }

    public void StopGameplay()
    {
        if (IsGameplayStopped) return;
        IsGameplayStopped = true;
        foreach (var listener in pauseListeners) listener.OnStopGameplay();
    }

    public void ResumeGameplay()
    {
        if (!IsGameplayStopped) return;
        IsGameplayStopped = false;
        foreach (var listener in pauseListeners) listener.OnResumeGameplay();
    }
}
