using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns level segments sequentially. Each segment is spawned in full, then the
/// sequencer waits until all spawned objects for that segment are gone before moving on.
///
/// Vertical alignment rules (row index increases upward on Y):
/// - Non-space: FIRST row is aligned to the spawn anchor Y.
/// - Space/Spacer: LAST row is aligned to the spawn anchor Y.
/// 
/// Space segments with an empty SpawnTable get a SegmentEndMarker injected at the last row,
/// so the space also ends reliably when the marker hits the bottom trigger.
/// </summary>
public class LevelSegmentSequencer : MonoBehaviour
{
    #region Inspector - Data

    [Header("Data")]
    [Tooltip("Default grid config when a segment has no stripes defined.")]
    [SerializeField] private GridConfig gridConfig;

    [Tooltip("LevelDefinition to spawn from (ordered segments).")]
    [SerializeField] private LevelDefinition levelDefinition;

    [Header("Enemy Colors (2D)")]
    [Tooltip("Palette used to resolve Enemy hue/tone to a Color at runtime.")]
    [SerializeField] private EnemyColorPalette enemyPalette;

    #endregion


    #region Inspector - Orchestrator

    [Header("Boss Adds (Orchestration)")]
    [Tooltip("Scene-level orchestrator that starts/stops add waves when boss enters Fight.")]
    [SerializeField] private BossAddsOrchestrator bossAddsOrchestrator;

    #endregion

    #region Inspector - Prefabs

    [Header("Prefabs")]
    [Tooltip("Lookup from SpawnType -> Prefab.")]
    [SerializeField] private SpawnPrefabCatalog prefabCatalog;

    [Tooltip("Fallback if catalog has no entry for a given SpawnType.")]
    [SerializeField] private GameObject defaultPrefab;

    #endregion

    #region Inspector - Spawn Anchor

    [Header("Spawn Anchor")]
    [Tooltip("Reference Y for vertical alignment on spawn.")]
    [SerializeField] private Transform segmentSpawnAnchor;

    [Tooltip("Fallback Y if no anchor Transform is assigned.")]
    [SerializeField] private float segmentSpawnStartY = 0f;

    #endregion

    #region Inspector - Runtime & Flow

    [Header("Runtime")]
    [Tooltip("Parent for all spawned runtime instances.")]
    [SerializeField] private Transform runtimeRoot;

    [Header("Flow Options")]
    [Tooltip("Start sequencing automatically on Start().")]
    [SerializeField] private bool autoRunOnStart = true;

    [Tooltip("If > 0, a segment will auto-complete after this many seconds even if objects remain (failsafe). 0 = disabled.")]
    [SerializeField, Range(0f, 600f)] private float segmentTimeoutSeconds = 0f;

    [Tooltip("Verbose logging for debugging.")]
    [SerializeField] private bool verboseLogging = false;

    #endregion

    #region Events

    public Action<int, LevelSegment> OnSegmentStarted;
    public Action<int, LevelSegment> OnSegmentEnded;
    public Action OnLevelEnded;

    #endregion

    #region Layout Cache

    private readonly List<int> segmentBaseWorldRows = new();
    private readonly List<List<StripeInfo>> segmentStripeMaps = new();

    #endregion

    #region Runtime State

    private int currentSegmentIndex = -1;
    private int activeCountForCurrentSegment = 0;
    private bool isRunning = false;
    private Coroutine runRoutine;

    #endregion

    #region Unity

    private void Start()
    {
        if (autoRunOnStart) Run();
    }

    private void OnDisable()
    {
        if (runRoutine != null)
        {
            StopCoroutine(runRoutine);
            runRoutine = null;
        }
        isRunning = false;
    }

    #endregion

    #region Public API

    public void Run()
    {
        if (isRunning) return;
        if (!ValidateInputs()) return;

        isRunning = true;
        RecomputeSegmentBases();
        RebuildStripeMaps();
        runRoutine = StartCoroutine(RunLevelCoroutine());
    }

    public void StopAndClear()
    {
        if (runRoutine != null)
        {
            StopCoroutine(runRoutine);
            runRoutine = null;
        }
        isRunning = false;
        ClearRuntime();
        currentSegmentIndex = -1;
        activeCountForCurrentSegment = 0;
    }

    public void ClearRuntime()
    {
        if (!runtimeRoot) return;
        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
        {
            var child = runtimeRoot.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    /// <summary>
    /// If some external system/payload spawns an object after the main pass,
    /// bind it to the current segment so it participates in completion.
    /// </summary>
    public void BindToCurrentSegment(GameObject go)
    {
        if (!go) return;
        if (currentSegmentIndex < 0) return;

        var tracker = go.GetComponent<SegmentObject>();
        if (tracker == null) tracker = go.AddComponent<SegmentObject>();
        tracker.Bind(this, currentSegmentIndex);

        activeCountForCurrentSegment++;
        if (verboseLogging) Debug.Log($"[Sequencer] External bind -> active={activeCountForCurrentSegment}");
    }

    #endregion

    #region Main Loop

    private IEnumerator RunLevelCoroutine()
    {
        var segments = levelDefinition.Segments;
        if (segments == null || segments.Count == 0)
        {
            Debug.LogWarning("[LevelSegmentSequencer] No segments in LevelDefinition.");
            isRunning = false;
            yield break;
        }

        for (int segIndex = 0; segIndex < segments.Count; segIndex++)
        {
            var segment = segments[segIndex];
            if (segment == null) continue;

            currentSegmentIndex = segIndex;
            activeCountForCurrentSegment = 0;

            SpawnEntireSegment(segIndex, segment);

            OnSegmentStarted?.Invoke(segIndex, segment);
            if (verboseLogging) Debug.Log($"[Sequencer] Segment {segIndex} started ({segment.SegmentType}).");

            float timer = 0f;
            while (activeCountForCurrentSegment > 0)
            {
                if (segmentTimeoutSeconds > 0f)
                {
                    timer += Time.deltaTime;
                    if (timer >= segmentTimeoutSeconds)
                    {
                        if (verboseLogging) Debug.LogWarning($"[Sequencer] Segment {segIndex} timeout reached. Forcing completion.");
                        break;
                    }
                }
                yield return null;
            }

            OnSegmentEnded?.Invoke(segIndex, segment);
            if (verboseLogging) Debug.Log($"[Sequencer] Segment {segIndex} ended.");
        }

        OnLevelEnded?.Invoke();
        if (verboseLogging) Debug.Log("[Sequencer] Level ended.");
        isRunning = false;
        runRoutine = null;
    }

    #endregion

    #region Spawning

    private void SpawnEntireSegment(int segIndex, LevelSegment segment)
    {
        // Working copy (so we can inject a marker for pure space)
        var table = segment.SpawnTable;
        var workingTable = (table != null) ? new List<SpawnEntry>(table) : new List<SpawnEntry>();

        if (segIndex >= segmentStripeMaps.Count) return;
        var stripes = segmentStripeMaps[segIndex];
        if (stripes == null || stripes.Count == 0) return;

        // Inject marker for pure Space segment with no entries
        EnsureSpaceMarker(segment, segIndex, stripes, workingTable);
        if (workingTable.Count == 0) return;

        int segBase = segmentBaseWorldRows[segIndex];

        // Temp root: spawn all children under it, shift once, then unparent
        var segRoot = new GameObject($"__Segment_{segIndex}_Root").transform;
        if (runtimeRoot) segRoot.SetParent(runtimeRoot, worldPositionStays: true);
        segRoot.position = Vector3.zero;

        foreach (var entry in workingTable)
        {
            if (entry == null) continue;

            int localRow = Mathf.Max(0, entry.rowOffset);
            int worldRow = segBase + localRow;

            // Stripe lookup
            StripeInfo stripe = default;
            bool foundStripe = false;
            for (int s = 0; s < stripes.Count; s++)
            {
                var st = stripes[s];
                if (worldRow >= st.worldStartRow && worldRow < st.worldEndRow)
                { stripe = st; foundStripe = true; break; }
            }
            if (!foundStripe)
            {
                if (verboseLogging) Debug.LogWarning($"[LevelSegmentSequencer] No stripe for worldRow={worldRow} in segment {segIndex}.");
                continue;
            }

            int rowInStripe = worldRow - stripe.worldStartRow;
            int col = Mathf.Clamp(entry.column, 0, stripe.config.Columns - 1);
            Vector3 spawnPos = GridStripeAdapter.GridToWorld(col, rowInStripe, stripe);

            bool isAnchorPayload = entry.payload != null && entry.payload.GetType().Name == "PivotAnchorDefinition";

            GameObject prefab = null;
            if (!isAnchorPayload)
            {
                if (prefabCatalog != null)
                    prefabCatalog.TryGetPrefab(entry.spawnType, out prefab);
                if (prefab == null) prefab = defaultPrefab;
            }

            GameObject go = null;
            SegmentObject tracker = null;

            if (prefab != null)
            {
                go = Instantiate(prefab, spawnPos, Quaternion.identity, segRoot);

                tracker = go.GetComponent<SegmentObject>();
                if (tracker == null) tracker = go.AddComponent<SegmentObject>();
                tracker.Bind(this, segIndex);

                activeCountForCurrentSegment++;

                // --- Tint 2D enemy via SpriteRenderer(s) ---
                if (entry.spawnType == SpawnType.Enemy && enemyPalette != null)
                {
                    if (entry.TryResolveEnemyColor(enemyPalette, out var resolvedColor))
                    {
                        ApplyEnemyColor2D(go, resolvedColor);
                    }
                }

                // === NEW: if this spawned object is a Boss, hook the orchestrator and late-bind segment ===
                TryHookBossAddsOrchestrator(go, tracker);
            }
            else if (isAnchorPayload)
            {
                // NEW: create a dummy under segRoot so anchors get the same alignment shift.
                go = new GameObject("__PivotAnchorDummy");
                go.transform.SetParent(segRoot, worldPositionStays: true);
                go.transform.position = spawnPos;

                // (Usually anchors don't participate in completion counting.)
            }
            else if (!isAnchorPayload && verboseLogging)
            {
                Debug.LogWarning($"[LevelSegmentSequencer] No prefab for {entry.spawnType} at col={col}, worldRow={worldRow}.");
            }

            var tags = entry.tags ?? new List<string>(0);
            Vector2Int gridCell = new Vector2Int(col, worldRow);
            entry.payload?.AttachTo(go, stripe.config, gridCell, tags);

            if (verboseLogging)
            {
                string kind = isAnchorPayload ? "ANCHOR" : "ENTITY";
                string payloadName = entry.payload ? entry.payload.name : "None";
                Debug.Log($"[Sequencer] {kind} {entry.spawnType} @ (c={col}, wr={worldRow}) seg={segment.SegmentType} stripe={stripe.topology} payload={payloadName}");
            }
        }

        // -------- Alignment (row index increases upward) --------
        int firstWorldRow = segBase;
        int lastWorldRow = segBase + Mathf.Max(0, segment.LengthInRows - 1);
        float firstY = GetWorldRowY(firstWorldRow, stripes);
        float lastY = GetWorldRowY(lastWorldRow, stripes);

        float deltaY;
        if (segment.SegmentType == SegmentType.Space)
        {
            // Space: normalize by FIRST row (keeps your original, working behavior)
            deltaY = -firstY;
        }
        else
        {
            // Non-space: align FIRST row to anchor
            float anchorY = GetAnchorY();
            deltaY = anchorY - firstY;
        }

        if (Mathf.Abs(deltaY) > Mathf.Epsilon)
            segRoot.position += new Vector3(0f, deltaY, 0f);

        // Reparent out of temp root
        for (int i = segRoot.childCount - 1; i >= 0; i--)
            segRoot.GetChild(i).SetParent(runtimeRoot, worldPositionStays: true);

#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(segRoot.gameObject);
        else Destroy(segRoot.gameObject);
#else
        Destroy(segRoot.gameObject);
#endif
    }

    /// <summary>
    /// If the instantiated object looks like a Boss, hook the scene orchestrator
    /// so it can manage add waves, and late-bind the SegmentObject to the boss controller.
    /// </summary>
    private void TryHookBossAddsOrchestrator(GameObject go, SegmentObject tracker)
    {
        if (!go) return;

        if (go.TryGetComponent<BossStateController>(out var boss))
        {
            // Let boss know its SegmentObject (late-bound)
            if (tracker != null)
                boss.BindSegmentObject(tracker);

            // Hook orchestrator if present (keeps boss prefab decoupled)
            if (bossAddsOrchestrator != null)
            {
                bossAddsOrchestrator.HookBoss(boss);
                if (verboseLogging) Debug.Log("[Sequencer] Boss detected -> Orchestrator hooked.");
            }
            else if (verboseLogging)
            {
                Debug.LogWarning("[Sequencer] Boss spawned but no BossAddsOrchestrator is assigned in the sequencer.");
            }
        }
    }

    #endregion

    #region Layout & Helpers

    private bool ValidateInputs()
    {
        if (gridConfig == null || levelDefinition == null)
        {
            Debug.LogWarning("[LevelSegmentSequencer] Assign GridConfig + LevelDefinition.");
            return false;
        }
        return true;
    }

    private void RecomputeSegmentBases()
    {
        segmentBaseWorldRows.Clear();
        int running = 0;
        foreach (var seg in levelDefinition.Segments)
        {
            segmentBaseWorldRows.Add(running);
            running += seg ? Mathf.Max(0, seg.LengthInRows) : 0;
        }
    }

    private void RebuildStripeMaps()
    {
        segmentStripeMaps.Clear();
        for (int i = 0; i < levelDefinition.Segments.Count; i++)
        {
            var seg = levelDefinition.Segments[i];
            int baseWorld = (i < segmentBaseWorldRows.Count) ? segmentBaseWorldRows[i] : 0;
            var stripes = SegmentStripeMap.Build(seg, baseWorld, gridConfig, RowStepY);
            segmentStripeMaps.Add(stripes);
        }
    }

    private float RowStepY(GridConfig cfg)
    {
        switch (cfg.Topology)
        {
            case GridTopology.Rectangle: return cfg.CellHeight;
            case GridTopology.Hex: return (cfg.HexOrientation == HexOrientation.PointyTop) ? 1.5f * cfg.HexSize : Mathf.Sqrt(3f) * cfg.HexSize;
            case GridTopology.Octagon: return 2f * cfg.OctApothem;
            default: return 1f;
        }
    }

    private float GetAnchorY()
    {
        if (segmentSpawnAnchor != null) return segmentSpawnAnchor.position.y;
        return segmentSpawnStartY;
    }

    /// <summary>Compute the world Y for a given worldRow using the segment's stripes.</summary>
    private float GetWorldRowY(int worldRow, List<StripeInfo> stripes)
    {
        for (int s = 0; s < stripes.Count; s++)
        {
            var st = stripes[s];
            if (worldRow >= st.worldStartRow && worldRow < st.worldEndRow)
            {
                int rowInStripe = worldRow - st.worldStartRow;
                // Column does not matter for Y
                return GridStripeAdapter.GridToWorld(0, rowInStripe, st).y;
            }
        }
        return 0f;
    }

    /// <summary>
    /// Add a tiny marker to Space segments with empty tables so they end reliably when reaching the bottom trigger.
    /// Places it at the last local row, middle column.
    /// </summary>
    private void EnsureSpaceMarker(LevelSegment segment, int segIndex, List<StripeInfo> stripes, List<SpawnEntry> workingTable)
    {
        if (!IsSpace(segment)) return;
        if (workingTable.Count > 0) return;

        int segBase = segmentBaseWorldRows[segIndex];
        int lastLocalRow = Mathf.Max(0, segment.LengthInRows - 1);
        int lastWorldRow = segBase + lastLocalRow;

        // Find stripe for last row
        StripeInfo stripe = default;
        bool found = false;
        for (int s = 0; s < stripes.Count; s++)
        {
            var st = stripes[s];
            if (lastWorldRow >= st.worldStartRow && lastWorldRow < st.worldEndRow)
            { stripe = st; found = true; break; }
        }
        if (!found) return;

        int midCol = Mathf.Max(0, stripe.config.Columns / 2);

        var entry = new SpawnEntry
        {
            rowOffset = lastLocalRow,
            column = midCol,
            spawnType = SpawnType.SegmentEndMarker,
            tags = new List<string>()
        };
        workingTable.Add(entry);
    }

    /// <summary>
    /// True if the segment is the "space" type.
    /// </summary>
    private bool IsSpace(LevelSegment segment)
    {
        return segment.SegmentType == SegmentType.Space;
    }

    #endregion

    #region SegmentObject callback

    internal void NotifyObjectDestroyed(int owningSegmentIndex)
    {
        if (!isRunning) return;
        if (owningSegmentIndex != currentSegmentIndex) return;

        activeCountForCurrentSegment = Mathf.Max(0, activeCountForCurrentSegment - 1);
        if (verboseLogging) Debug.Log($"[Sequencer] Segment {owningSegmentIndex} active-- => {activeCountForCurrentSegment}");
    }

    #endregion

    #region Enemy Color (2D)

    /// <summary>
    /// Tints all SpriteRenderers under the enemy. Preserves original alpha.
    /// </summary>
    private void ApplyEnemyColor2D(GameObject root, Color color)
    {
        if (!root) return;
        var spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var spriteRenderer = spriteRenderers[i];
            var alpha = spriteRenderer.color.a;
            spriteRenderer.color = new Color(color.r, color.g, color.b, alpha);
        }
    }

    #endregion
}
