using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PauseableRigidbody2D : MonoBehaviour, IStoppable
{
    [SerializeField] private bool clearVelocityOnStop = false;

    private Rigidbody2D rigidBody;
    private Vector2 storedVelocity;
    private float storedAngularVelocity;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
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
        if (!rigidBody) return;
        storedVelocity = rigidBody.linearVelocity;
        storedAngularVelocity = rigidBody.angularVelocity;

        if (clearVelocityOnStop)
        {
            rigidBody.linearVelocity = Vector2.zero;
            rigidBody.angularVelocity = 0f;
        }

        rigidBody.simulated = false; // full freeze, even with timeScale=1
    }

    public void OnResumeGameplay()
    {
        if (!rigidBody) return;
        rigidBody.simulated = true;
        if (!clearVelocityOnStop)
        {
            rigidBody.linearVelocity = storedVelocity;
            rigidBody.angularVelocity = storedAngularVelocity;
        }
    }

    private void OnStopGameplayIfPaused()
    {
        if (PauseManager.Instance.IsGameplayStopped) OnStopGameplay();
    }
}
