using UnityEngine;

/// <summary>
/// Moves along a regular hexagon around 'center' in the XY plane (2D top-down).
/// Follows straight edges (edgeSpeed) and briefly pauses at corners (cornerHold).
/// Robust: uses edge param (edgeT) instead of world-position dot tests.
/// </summary>
[DisallowMultipleComponent]
public class HexOrbitFollower : MonoBehaviour, IStageActivatable
{
    #region Serialized
    [SerializeField, Tooltip("Transform to orbit around (boss).")]
    private Transform center;

    [SerializeField, Tooltip("Hex radius (center to each corner, in world units).")]
    private float radius = 3.5f;

    [SerializeField, Tooltip("Units per second along each straight edge.")]
    private float edgeSpeed = 4f;

    [SerializeField, Tooltip("Seconds to pause at every corner.")]
    private float cornerHold = 0.05f;

    [SerializeField, Range(0f, 1f), Tooltip("Starting position as fraction of the full loop (0..1).")]
    private float phaseOffset = 0f;

    [SerializeField, Tooltip("Rotation offset where 0 means FLAT TOP edge; positive rotates CCW.")]
    private float rotationOffsetDeg = 0f;

    [SerializeField, Tooltip("If true, traverse corners clockwise. Else counter-clockwise.")]
    private bool clockwise = true;

    [SerializeField, Tooltip("If true, recompute corners when center moves.")]
    private bool stickToCenter = true;
    #endregion

    #region Private
    private bool isActive;
    private Vector3[] corners = new Vector3[6];
    private int currentCornerIndex;
    private float pauseTimer;

    // Parametric progress along current edge [0..1]
    private float edgeT;

    // Cache to only rebuild when needed
    private Vector3 lastCenterPos = new Vector3(float.NaN, float.NaN, float.NaN);
    #endregion

    #region Initialization
    /// <summary>Configure and arm the follower. Call right after spawning.</summary>
    public void Initialize(Transform center, float radius, float edgeSpeed, float cornerHold,
                           float phaseOffset, float rotationOffsetDeg = 0f, bool clockwise = true)
    {
        this.center = center;
        this.radius = Mathf.Max(0.001f, radius);
        this.edgeSpeed = Mathf.Max(0.001f, edgeSpeed);
        this.cornerHold = Mathf.Max(0f, cornerHold);
        this.phaseOffset = Mathf.Repeat(phaseOffset, 1f);
        this.rotationOffsetDeg = rotationOffsetDeg; // 0 = flat top
        this.clockwise = clockwise;

        ForceRecomputeCorners();

        // Place on corresponding edge by phase
        float fIndex = this.phaseOffset * 6f;
        currentCornerIndex = Mathf.FloorToInt(fIndex) % 6;
        edgeT = fIndex - Mathf.Floor(fIndex);

        Vector3 a = corners[currentCornerIndex];
        Vector3 b = corners[(currentCornerIndex + 1) % 6];
        transform.position = Vector3.LerpUnclamped(a, b, edgeT);

        isActive = true;
        pauseTimer = 0f;
    }
    #endregion

    #region Unity
    private void Update()
    {
        if (!isActive || center == null) return;

        // Rebuild only if center moved (prevents jitter/teleport)
        if (stickToCenter)
        {
            if (!ApproximatelyEqual(center.position, lastCenterPos))
            {
                RecomputeCorners();
                // Re-anchor to current edge using edgeT:
                Vector3 a = corners[currentCornerIndex];
                Vector3 b = corners[(currentCornerIndex + 1) % 6];
                transform.position = Vector3.LerpUnclamped(a, b, edgeT);
            }
        }

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }

        Vector3 p = transform.position;
        Vector3 a0 = corners[currentCornerIndex];
        Vector3 b0 = corners[(currentCornerIndex + 1) % 6];

        float edgeLen = (b0 - a0).magnitude;
        if (edgeLen <= 0.0001f)
        {
            AdvanceCorner();
            return;
        }

        // Advance param along edge by speed/length
        float dt = Time.deltaTime;
        edgeT += (edgeSpeed * dt) / edgeLen;

        if (edgeT >= 1f)
        {
            // Snap to corner and pause, then go to next edge
            transform.position = b0;
            edgeT -= 1f;
            pauseTimer = cornerHold;
            AdvanceCorner();
        }
        else
        {
            transform.position = Vector3.LerpUnclamped(a0, b0, edgeT);
        }
    }
    #endregion

    #region Helpers
    private void AdvanceCorner()
    {
        currentCornerIndex = (currentCornerIndex + 1) % 6;
    }

    private void ForceRecomputeCorners()
    {
        lastCenterPos = center ? center.position : Vector3.zero;
        ComputeHexCornersInto(ref corners, lastCenterPos, radius, rotationOffsetDeg, clockwise);
    }

    private void RecomputeCorners()
    {
        lastCenterPos = center.position;
        ComputeHexCornersInto(ref corners, lastCenterPos, radius, rotationOffsetDeg, clockwise);
    }

    private static bool ApproximatelyEqual(Vector3 a, Vector3 b)
    {
        // Cheap compare, good enough for this use
        return Mathf.Abs(a.x - b.x) < 0.0001f &&
               Mathf.Abs(a.y - b.y) < 0.0001f &&
               Mathf.Abs(a.z - b.z) < 0.0001f;
    }

    /// <summary>Static utility for editor gizmos or other systems.</summary>
    public static Vector3[] ComputeHexCorners(Vector3 center, float radius, float rotationOffsetDeg, bool clockwise)
    {
        Vector3[] arr = new Vector3[6];
        ComputeHexCornersInto(ref arr, center, radius, rotationOffsetDeg, clockwise);
        return arr;
    }

    /// <summary>
    /// Computes corners. IMPORTANT: rotationOffsetDeg = 0 means FLAT TOP.
    /// We implement that by starting at 30° and adding rotationOffsetDeg.
    /// </summary>
    private static void ComputeHexCornersInto(ref Vector3[] arr, Vector3 center, float radius, float rotationOffsetDeg, bool clockwise)
    {
        if (arr == null || arr.Length != 6) arr = new Vector3[6];

        // Base = 30° → flat top; add user rotation
        float baseDeg = 30f + rotationOffsetDeg;

        // Math angles grow CCW. For "clockwise=true" we step NEGATIVE.
        int stepSign = clockwise ? -1 : 1;

        for (int i = 0; i < 6; i++)
        {
            float ang = baseDeg + stepSign * 60f * i;
            float rad = Mathf.Deg2Rad * ang;
            arr[i] = center + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        }
    }
    #endregion

    #region IStageActivatable
    public void PauseMover() => isActive = false;
    public void ResumeMover() => isActive = true;
    public void ArmAtEntry(Vector3 entryWorldPos) { /* not used */ }
    #endregion

#if UNITY_EDITOR
    #region Editor Gizmos
    private void OnDrawGizmosSelected()
    {
        Transform c = center ? center : transform;
        Vector3[] drawCorners = ComputeHexCorners(c.position, Mathf.Max(0.001f, radius), rotationOffsetDeg, clockwise);

        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        for (int i = 0; i < 6; i++)
        {
            Vector3 a = drawCorners[i];
            Vector3 b = drawCorners[(i + 1) % 6];
            Gizmos.DrawLine(a, b);
        }
        Gizmos.DrawWireSphere(c.position, 0.1f);
    }
    #endregion
#endif
}
