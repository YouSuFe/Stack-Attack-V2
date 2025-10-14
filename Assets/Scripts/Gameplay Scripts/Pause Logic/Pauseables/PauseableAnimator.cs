using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PauseableAnimator : MonoBehaviour, IStoppable
{
    private Animator animator;
    private float storedSpeed = 1f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
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
        if (!animator) return;
        storedSpeed = animator.speed;
        animator.speed = 0f;
    }

    public void OnResumeGameplay()
    {
        if (!animator) return;
        animator.speed = storedSpeed;
    }

    private void OnStopGameplayIfPaused()
    {
        if (PauseManager.Instance.IsGameplayStopped) OnStopGameplay();
    }
}
