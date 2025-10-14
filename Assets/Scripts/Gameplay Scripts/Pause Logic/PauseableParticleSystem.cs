using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class PauseableParticleSystem : MonoBehaviour, IStoppable
{
    private ParticleSystem VfxParticles;

    private void Awake()
    {
        VfxParticles = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        PauseManager.Instance?.Register(this);
        if (PauseManager.Instance) OnStopGameplayIfPaused();
    }

    private void OnDisable()
    {
        PauseManager.Instance?.Unregister(this);
    }

    public void OnStopGameplay()
    {
        if (VfxParticles != null) VfxParticles.Pause(true); // include children
    }

    public void OnResumeGameplay()
    {
        if (VfxParticles != null) VfxParticles.Play(true);
    }

    private void OnStopGameplayIfPaused()
    {
        if (PauseManager.Instance.IsGameplayStopped) OnStopGameplay();
    }
}
