using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes level progress using the per-segment GAP model and spawns a dummy that
/// your Conveyor moves. Exposes a normalized progress value (Progress01) for UI.
/// 
/// GAP MODEL:
///   gap = spawnPoint.y - referencePoint.y
///   totalDistanceWorld = (segmentsBeforeBossStart * gap) + (rowsToBossStart * rowHeight)
/// Progress is computed from the dummy's current vertical distance to the reference.
/// </summary>
public class LevelProgressRuntime : MonoBehaviour
{
    #region Helper Data
    [Serializable]
    public struct ProgressMarker
    {
        public int SegmentIndex;   // Index in LevelDefinition
        public int StartRow;       // Cumulative rows BEFORE this segment starts
        public SegmentType Type;   // EnemyWave / Reward / Boss
    }

    [Serializable]
    public struct LevelProgressPlan
    {
        public int TotalRowsToBossStart;        // Rows up to boss START (excludes boss rows)
        public int SegmentsBeforeBossStart;     // Number of segments strictly before boss
        public int[] SegmentStartRows;          // Starting cumulative row for each segment
        public ProgressMarker[] Markers;        // Enemy/Reward/Boss starts (ordered by segment index)
    }
    #endregion

    #region Serialized Fields
    [Header("Level & World References")]
    [SerializeField, Tooltip("LevelDefinition used by your sequencer.")]
    private LevelDefinition levelDefinition;

    [SerializeField, Tooltip("World spawn point where each segment's first row is aligned.")]
    private Transform spawnPoint;

    [SerializeField, Tooltip("World reference (usually the player). Must remain fixed in Y.")]
    private Transform referencePoint;

    [Header("Dummy")]
    [SerializeField, Tooltip("Prefab with your SpawnStageAgent so the Conveyor moves it. This script never moves it.")]
    private GameObject dummyPrefab;

    [Header("Tuning")]
    [SerializeField, Tooltip("Unity units per row. Your grid uses 1.")]
    private float rowHeight = 1f;
    #endregion

    #region Public API
    /// <summary>Normalized progress [0..1] based on dummy position vs totalDistanceWorld.</summary>
    public float Progress01 { get; private set; }

    /// <summary>
    /// Returns the current level progress as an integer percentage [0–100].
    /// </summary>
    public int ProgressPercent => Mathf.RoundToInt(Progress01 * 100f);

    /// <summary>Total distance the progress covers in world units (from start to boss start).</summary>
    public float TotalDistanceWorld => totalDistanceWorld;

    /// <summary>The vertical gap between spawn and reference: spawnY - referenceY.</summary>
    public float GapSpawnToReference => gapSpawnToReference;

    /// <summary>Row height in world units (copied to UI for marker placement math).</summary>
    public float RowHeight => rowHeight;

    /// <summary>Computed plan (rows to boss, segment starts, and markers).</summary>
    public LevelProgressPlan Plan => plan;

    /// <summary>The spawned dummy (moved by your Conveyor). Can be null if no prefab assigned.</summary>
    public Transform DummyInstance => dummyInstance;

    public bool IsInitialized { get; private set; }

    public event Action OnInitialized;

    #endregion

    #region Private State
    private LevelProgressPlan plan;
    private float gapSpawnToReference; // spawnY - referenceY
    private float totalDistanceWorld;  // segmentsBeforeBoss * gap + rowsToBoss * rowHeight
    private Transform dummyInstance;   // moved by your Conveyor (via SpawnStageAgent)
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (!ValidateRefs())
        {
            enabled = false;
            return;
        }

        // Build per-level plan (rows & markers)
        plan = BuildPlan(levelDefinition);

        // Compute the GAP (how far spawn is above/below the reference)
        gapSpawnToReference = spawnPoint.position.y - referencePoint.position.y;

        // Core distance calculation (your model):
        // - We incur 'gap' once per segment before the boss
        // - We add world height for all rows before the boss
        totalDistanceWorld =
            Mathf.Max(0f, (plan.SegmentsBeforeBossStart * gapSpawnToReference) +
                           (plan.TotalRowsToBossStart * rowHeight));

        // Spawn the dummy so the Conveyor will move it downward. We place it at the
        // top such that when it reaches the reference Y, progress becomes 1.
        SpawnDummyAtStartY();

        // Initialize progress immediately
        UpdateProgress();

        IsInitialized = true;
        OnInitialized?.Invoke();
    }

    private void Update()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsGameplayStopped)
            return;

        UpdateProgress();
    }
    #endregion

    #region Planning
    /// <summary>
    /// Reads LevelDefinition and computes:
    ///  - rows-to-boss-start,
    ///  - segment start rows,
    ///  - markers (Enemy/Reward/Boss starts).
    /// Boss start is treated as the endpoint of the progress span.
    /// </summary>
    private LevelProgressPlan BuildPlan(LevelDefinition def)
    {
        var p = new LevelProgressPlan
        {
            TotalRowsToBossStart = 0,
            SegmentsBeforeBossStart = 0
        };

        if (def == null || def.Segments == null || def.Segments.Count == 0)
        {
            p.SegmentStartRows = Array.Empty<int>();
            p.Markers = Array.Empty<ProgressMarker>();
            return p;
        }

        int count = def.Segments.Count;
        p.SegmentStartRows = new int[count];

        var markersList = new List<ProgressMarker>(count);

        int cumulativeRows = 0;
        bool bossSeen = false;

        for (int i = 0; i < count; i++)
        {
            var seg = def.Segments[i];
            if (seg == null) continue;

            // Record the cumulative row count BEFORE this segment – this is its start row.
            p.SegmentStartRows[i] = cumulativeRows;

            // We only mark Enemy/Reward/Boss starts.
            if (seg.SegmentType == SegmentType.EnemyWave ||
                seg.SegmentType == SegmentType.Reward ||
                seg.SegmentType == SegmentType.Boss)
            {
                markersList.Add(new ProgressMarker
                {
                    SegmentIndex = i,
                    StartRow = cumulativeRows,
                    Type = seg.SegmentType
                });
            }

            // The first Boss segment's start is the logical end of the progress span.
            if (seg.SegmentType == SegmentType.Boss && !bossSeen)
            {
                p.TotalRowsToBossStart = cumulativeRows;
                p.SegmentsBeforeBossStart = i;
                bossSeen = true;
            }

            // Advance by this segment's height in rows.
            cumulativeRows += Mathf.Max(0, seg.LengthInRows);
        }

        // If there's no boss, measure to the very end.
        if (!bossSeen)
        {
            p.TotalRowsToBossStart = cumulativeRows;
            p.SegmentsBeforeBossStart = count;
        }

        p.Markers = markersList.ToArray();
        return p;
    }
    #endregion

    #region Dummy & Progress
    /// <summary>
    /// Spawns the dummy at referenceY + totalDistanceWorld so that, as the Conveyor
    /// moves it down, its Y distance to reference decreases from totalDistance to 0.
    /// </summary>
    private void SpawnDummyAtStartY()
    {
        if (dummyPrefab == null) return;

        float startY = referencePoint.position.y + totalDistanceWorld;
        Vector3 startPos = new Vector3(
            spawnPoint.position.x,   // keep aligned with your spawn X (optional)
            startY,
            spawnPoint.position.z
        );

        var go = Instantiate(dummyPrefab, startPos, Quaternion.identity);
        dummyInstance = go.transform;
    }

    /// <summary>
    /// Computes normalized progress from dummy's vertical distance to reference:
    ///   remaining = max(0, dummyY - referenceY)
    ///   Progress01 = 1 - remaining / totalDistanceWorld
    /// Guards against zero/near-zero totalDistanceWorld.
    /// </summary>
    private void UpdateProgress()
    {
        if (totalDistanceWorld <= 0.0001f)
        {
            Progress01 = 1f;
            return;
        }

        if (dummyInstance == null)
        {
            // If there's no dummy, we cannot infer movement; keep progress at 0.
            Progress01 = 0f;
            return;
        }

        float remaining = Mathf.Max(0f, dummyInstance.position.y - referencePoint.position.y);
        Progress01 = Mathf.Clamp01(1f - (remaining / totalDistanceWorld));
    }
    #endregion

    #region Utilities
    private bool ValidateRefs()
    {
        bool ok = true;

        if (levelDefinition == null) { Debug.LogError("LevelProgressRuntime: LevelDefinition is not assigned."); ok = false; }
        if (spawnPoint == null) { Debug.LogError("LevelProgressRuntime: SpawnPoint is not assigned."); ok = false; }
        if (referencePoint == null) { Debug.LogError("LevelProgressRuntime: ReferencePoint is not assigned."); ok = false; }

        return ok;
    }
    #endregion
}
