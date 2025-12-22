using UnityEngine;

[DisallowMultipleComponent]
public class ZigZagHorizontalMover : MonoBehaviour, IStageActivatable, IPausable
{
    #region Inspector
    [Tooltip("Horizontal amplitude (world units).")]
    [SerializeField] private float amplitude = 1f;

    [Tooltip("Oscillation frequency (cycles/sec).")]
    [SerializeField] private float frequency = 1.25f;

    [Tooltip("Additional downward speed (units/sec).")]
    [SerializeField] private float verticalSpeed = 0f;
    #endregion

    #region Private
    // Captured at the entry gate (NOT at spawn) to keep visuals consistent.
    private float startX;
    private float timer;
    private bool isActive = false;
    private bool isPaused = false;

    #endregion


    private void OnEnable()
    {
        if (PauseManager.Instance != null)
            PauseManager.Instance.Register(this);
        timer = 0f;

    }

    private void OnDisable()
    {
        if (PauseManager.Instance != null)
            PauseManager.Instance.Unregister(this);
    }

    public void OnStopGameplay() => isPaused = true;

    public void OnResumeGameplay() => isPaused = false;

    private void Update()
    {
        if (!isActive) return;
        if (isPaused) return;

        timer += Time.deltaTime;

        float x = Mathf.Sin(timer * Mathf.PI * 2f * frequency) * amplitude;
        var pos = transform.position;
        pos.x = startX + x;

        if (verticalSpeed > 0f)
            pos += Vector3.down * (verticalSpeed * Time.deltaTime);

        transform.position = pos;
    }

    #region Public API
    public void SetParameters(float amp, float freq, float vSpeed)
    {
        amplitude = Mathf.Max(0f, amp);
        frequency = Mathf.Max(0f, freq);
        verticalSpeed = Mathf.Max(0f, vSpeed);
    }
    #endregion

    #region IStageActivatable
    /// <summary>Stop applying zigzag movement while the object is off-screen/staging.</summary>
    public void PauseMover()
    {
        isActive = false;
    }

    /// <summary>
    /// Capture baseline at the entry gate so sine motion begins correctly on-screen.
    /// </summary>
    public void ArmAtEntry(Vector3 entryWorldPos)
    {
        startX = entryWorldPos.x;
        // Starting the wave "fresh" on entry keeps visuals deterministic.
        timer = 0f;
    }

    /// <summary>Begin applying zigzag movement once on-screen.</summary>
    public void ResumeMover()
    {
        isActive = true;
    }
    #endregion
}
