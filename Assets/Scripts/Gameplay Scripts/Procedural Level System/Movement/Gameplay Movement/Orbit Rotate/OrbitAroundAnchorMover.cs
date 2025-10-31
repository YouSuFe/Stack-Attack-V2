using UnityEngine;

[DisallowMultipleComponent]
public class OrbitAroundAnchorMover : MonoBehaviour, IStageActivatable
{
    #region Inspector
    [SerializeField] private string anchorKey;
    [SerializeField] private float radiusOverride = -1f;
    [SerializeField] private float phaseOffsetDeg = 0f;   // final relative offset to anchor.AngleDeg
    [SerializeField] private float personalVerticalSpeed = 0f;

    [SerializeField, Tooltip("Capture phase from entry position instead of spawn.")]
    private bool lockPhaseFromSpawn = true;
    #endregion

    #region Private
    private PivotAnchor anchor;
    private float radius;
    private float personalY;
    private bool isActive = false;
    private bool armed = false;       // baseline captured?
    #endregion

    #region Unity
    private void OnEnable()
    {
        // Try immediate bind
        TryBindAnchor();

        // If anchor not ready yet, listen for late registration
        if (anchor == null)
        {
            PivotAnchor.AnchorRegistered -= HandleAnchorRegistered; // avoid dupes
            PivotAnchor.AnchorRegistered += HandleAnchorRegistered;
        }
    }

    private void OnDisable()
    {
        if (anchor != null)
        {
            anchor.UnregisterFollower(this);
            anchor = null;
        }

        PivotAnchor.AnchorRegistered -= HandleAnchorRegistered;
    }

    private void Update()
    {
        if (!isActive || anchor == null) return;

        // Optional per-unit descent
        if (personalVerticalSpeed != 0f)
            personalY += personalVerticalSpeed * Time.deltaTime;

        // Orbit around anchor
        float angleDeg = anchor.AngleDeg + phaseOffsetDeg;
        float rad = angleDeg * Mathf.Deg2Rad;

        Vector2 radial = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        Vector3 target = anchor.transform.position + new Vector3(radial.x, radial.y, 0f);
        target.y -= personalY;
        transform.position = target;
    }
    #endregion

    #region Late-binding helpers
    private void TryBindAnchor()
    {
        if (string.IsNullOrEmpty(anchorKey)) return;

        var a = PivotAnchor.Find(anchorKey);
        if (a != null)
        {
            anchor = a;
            anchor.RegisterFollower(this);
        }
    }

    private void HandleAnchorRegistered(string key, PivotAnchor a)
    {
        if (anchor != null) return;
        if (string.IsNullOrEmpty(anchorKey)) return;
        if (!string.Equals(key, anchorKey)) return;

        anchor = a;
        anchor.RegisterFollower(this);

        // If we were already armed and anchor is active, we can start immediately.
        if (armed && anchor.IsActivated)
            isActive = true;
    }
    #endregion

    #region IStageActivatable
    public void PauseMover()
    {
        isActive = false;
    }

    public void ArmAtEntry(Vector3 entryWorldPos)
    {
        // Ensure anchor binding in case it appeared just now
        if (anchor == null) TryBindAnchor();

        if (anchor == null)
        {
            // No anchor yet; we’ll complete baselines once it appears (handled in ResumeMover).
            armed = true;
            return;
        }

        // Compute radius baseline: prefer explicit override, else use world delta at entry.
        Vector3 delta3 = entryWorldPos - anchor.transform.position;
        Vector2 delta = new Vector2(delta3.x, delta3.y);
        radius = (radiusOverride > 0f) ? radiusOverride : delta.magnitude;

        // Compute phase to keep follower where it visually appears at entry.
        if (lockPhaseFromSpawn)
        {
            float worldAngleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            phaseOffsetDeg = Mathf.DeltaAngle(anchor.AngleDeg, worldAngleDeg);
        }

        personalY = 0f; // reset personal descent reference at the gate
        armed = true;
    }

    public void ResumeMover()
    {
        if (!armed) { isActive = false; return; }

        if (anchor == null)
        {
            // Still no anchor; keep waiting. On late registration we’ll start if activated.
            isActive = false;
            return;
        }

        if (!anchor.IsActivated)
        {
            // Wait for pivot to finish its drag (activation happens after handoff)
            anchor.OnActivated -= HandleAnchorActivated;
            anchor.OnActivated += HandleAnchorActivated;
            isActive = false;
            return;
        }

        isActive = true;
    }

    private void HandleAnchorActivated()
    {
        if (anchor != null)
            anchor.OnActivated -= HandleAnchorActivated;

        if (armed) isActive = true;
    }
    #endregion

    #region Public API (optional)
    /// <summary>
    /// Update runtime parameters (kept for compatibility with Definition).
    /// </summary>
    public void Configure(string anchorKey, float radiusOverride, float phaseOffsetDeg, float personalV, bool lockPhaseFromSpawn)
    {
        this.anchorKey = anchorKey;
        this.radiusOverride = radiusOverride;
        this.phaseOffsetDeg = phaseOffsetDeg;
        this.personalVerticalSpeed = personalV;
        this.lockPhaseFromSpawn = lockPhaseFromSpawn;

        // Rebind if possible; baselines will be captured in ArmAtEntry.
        anchor = PivotAnchor.Find(anchorKey);
        armed = false;
    }
    #endregion
}
