#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Feature-complete grid painter for LevelDefinition segments:
/// - Auto/Manual segment targeting
/// - Single / Line / Rect brushes (+ horizontal/vertical lock for lines)
/// - Count/Spacing stacks
/// - Erase & right-click erase
/// - Avoid duplicates
/// - On-Conflict action (Skip / Overwrite Payload+Tags / Overwrite All)
/// - Eyedropper (press 'I' over a cell) to copy SpawnType + Payload + Tags into the brush
/// - Movement payload (MovementDefinition) + Tags per cell
/// - Structured Tags UI (anchor/pivot/phase/radius) with CSV fallback
/// - Restrict-to-segment-bounds + Disallow painting in Spacer
/// - Validate & Fix + Auto-migrate (preserves tags/payloads)
/// - Status banner
/// - [HEX] Hex topology support: Single / Line / Area(radius), hex previews & hit-test
/// </summary>
public class GridLevelPainterWindow : EditorWindow
{
    #region Serialized Targets
    [Tooltip("Grid configuration ScriptableObject (columns, cell size, visible rows/buffers…).")]
    [SerializeField] private GridConfig gridConfig;

    [Tooltip("LevelDefinition ScriptableObject that contains ordered level segments to paint into.")]
    [SerializeField] private LevelDefinition levelDefinition;
    #endregion

    #region Stripe State (Painter)
    private readonly List<List<StripeInfo>> segmentStripeMaps = new List<List<StripeInfo>>();
    #endregion

    #region Modes & Interaction
    private enum TargetMode { AutoFromClick, ManualSegment }
    private enum BrushMode { Single, Line, RectOrArea } // [HEX] Rect for Rectangle; Area(radius) for Hex
    private enum LineAxis { Auto, Horizontal, Vertical }

    [Tooltip("Auto: clicked row decides the segment.\nManual: paint into the selected segment index.")]
    [SerializeField] private TargetMode targetMode = TargetMode.AutoFromClick;

    [Tooltip("Which brush shape to use when painting.")]
    [SerializeField] private BrushMode brushMode = BrushMode.Single;

    [Tooltip("When Brush Mode is Line, this constrains the line to horizontal or vertical, or lets it auto-align by drag (Rectangle only).")]
    [SerializeField] private LineAxis lineAxis = LineAxis.Auto;

    [Tooltip("Manual segment index to edit when Target Mode = Manual Segment.\nUse number keys 1–9 in Scene view to switch quickly.")]
    [SerializeField] private int manualSegmentIndex = 0;

    [Tooltip("When enabled, right-click acts as erase, independent of the Erase Mode toggle.")]
    [SerializeField] private bool rightClickErases = true;
    #endregion

    #region Brush Settings + Conflict
    private enum OnConflictAction { Skip, OverwritePayloadAndTags, OverwriteAll }

    [Tooltip("SpawnType to assign when painting cells.")]
    [SerializeField] private SpawnType brushSpawnType = SpawnType.Enemy;

    // Enemy color brush state (used only when Spawn Type == Enemy)
    [SerializeField] private EnemyColorPalette enemyPalette;
    [SerializeField] private EnemyHue brushEnemyHue = EnemyHue.Red;
    [SerializeField] private EnemyTone brushEnemyTone = EnemyTone.Normal;

    [Tooltip("Number of repeated placements per painted cell (stacked down rows using Spacing).")]
    [SerializeField] private int brushCount = 1;

    [Tooltip("Row spacing between repeated placements when Count > 1.")]
    [SerializeField] private int brushSpacingRows = 1;

    [Tooltip("If enabled, the brush removes matching entries instead of adding them.")]
    [SerializeField] private bool eraseMode = false;

    [Tooltip("Skip painting a new entry if a matching one already exists in the same cell for the same SpawnType.")]
    [SerializeField] private bool avoidDuplicates = true;

    [Tooltip("What to do when painting a cell that already has an entry at (col,row).")]
    [SerializeField] private OnConflictAction onConflict = OnConflictAction.Skip;

    [Tooltip("Optional movement payload (MovementDefinition or Composite) assigned to each painted entry.")]
    [SerializeField] private MovementDefinition movementDefinition;

    [SerializeField, Tooltip("Enemy/Boss stats template written to entries when type is Enemy or Boss.")]
    private EnemyDefinition brushEnemyDefinition;

    // Free-form fallback
    [Tooltip("Legacy: comma-separated tags to store per entry.\nExamples: 'anchor=RingA, pivot=here'  or  'anchor=RingA, phase=90, r=3.5'")]
    [SerializeField] private string brushTagsCsv = "";
    #endregion

    #region Structured Tags (existing)
    [SerializeField] private bool useStructuredTags = true;
    [SerializeField] private bool showStructuredTags = true;

    // Common
    private string st_anchorKey = "RingA";

    // Pivot
    private enum PivotMode { None, Here, Grid }
    private PivotMode st_pivotMode = PivotMode.None;
    private int st_pivotCol = 0, st_pivotRow = 0;

    // Orbit
    private float st_phaseDeg = 0f;
    private float st_radius = -1f; // <=0 = not set
    #endregion

    #region Restrictions
    [Tooltip("Keep painting confined to the world-row range of the active segment during a drag.")]
    [SerializeField] private bool restrictToSegmentBounds = true;

    [Tooltip("Block painting into Spacer segments (by SegmentType == Spacer or name contains 'Spacer').")]
    [SerializeField] private bool disallowPaintingInSpacer = true;
    #endregion

    #region [HEX] Hex-only Brush Settings
    [Tooltip("[Hex] Radius of Area brush. In Hex topology, Rect becomes a Disk (radius).")]
    [SerializeField, Range(0, 48)] private int hexAreaRadius = 0;

    [Tooltip("[Hex] Hold Shift while dragging Line to snap to nearest of 6 hex directions.")]
    [SerializeField] private bool hexLineSnap6Dirs = true;
    #endregion

    #region Scene Interaction State
    private bool sceneHooked;
    private bool isDragging;
    private Vector2Int dragStartCell, dragEndCell;
    private int dragSegmentIndex = -1;
    private int dragLocalRowStart = -1;
    private string dragSegmentName = "";
    #endregion

    #region Layout Cache
    private readonly List<int> segmentBaseWorldRows = new();
    private int totalWorldRows = 0;
    #endregion

    #region Status Banner
    private double statusUntilTime;
    private string statusMessage;
    private MessageType statusType;
    #endregion

    #region Menu / Lifecycle
    [MenuItem("Window/Level/Grid Level Painter")]
    public static void ShowWindow()
    {
        var window = GetWindow<GridLevelPainterWindow>("Grid Level Painter");
        window.minSize = new Vector2(560, 460);
    }

    private void OnEnable() { HookScene(true); }
    private void OnDisable() { HookScene(false); }
    #endregion

    #region Scene Hook
    private void HookScene(bool on)
    {
        if (on && !sceneHooked) { SceneView.duringSceneGui += DuringSceneGUI; sceneHooked = true; }
        else if (!on && sceneHooked) { SceneView.duringSceneGui -= DuringSceneGUI; sceneHooked = false; }
    }
    #endregion

    #region GUI
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
        gridConfig = (GridConfig)EditorGUILayout.ObjectField(
            new GUIContent("Grid Config", "Grid configuration ScriptableObject (columns, cell size, visible rows/buffers…)."),
            gridConfig, typeof(GridConfig), false);

        levelDefinition = (LevelDefinition)EditorGUILayout.ObjectField(
            new GUIContent("Level Definition", "LevelDefinition ScriptableObject that contains ordered level segments to paint into."),
            levelDefinition, typeof(LevelDefinition), false);

        if (gridConfig != null && levelDefinition != null)
        {
            RecomputeSegmentBases();
            RebuildStripeMaps(); // NEW (build stripe cache after bases)
        }
        using (new EditorGUI.DisabledScope(levelDefinition == null))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Modes", EditorStyles.boldLabel);
            targetMode = (TargetMode)EditorGUILayout.EnumPopup(
                new GUIContent("Target Mode", "Auto: clicked row decides the segment.\nManual: paint into the selected segment index."),
                targetMode);

            using (new EditorGUI.DisabledScope(targetMode != TargetMode.ManualSegment))
            {
                int segCount = levelDefinition?.Segments?.Count ?? 0;
                if (segCount == 0)
                {
                    EditorGUILayout.HelpBox("This LevelDefinition has no segments.", MessageType.Info);
                    if (GUILayout.Button("Create First Segment (EnemyWave, 30 rows)")) CreateFirstSegment();
                }
                else
                {
                    manualSegmentIndex = Mathf.Clamp(manualSegmentIndex, 0, segCount - 1);
                    manualSegmentIndex = EditorGUILayout.IntSlider(
                        new GUIContent("Manual Segment Index", "Index into LevelDefinition.Segments to edit when in Manual mode.\nShortcuts: 1–9 in Scene view."),
                        manualSegmentIndex, 0, segCount - 1);

                    var seg = levelDefinition.Segments[manualSegmentIndex];
                    if (seg != null)
                    {
                        EditorGUILayout.LabelField($"Segment[{manualSegmentIndex}] {seg.SegmentType}   Length: {seg.LengthInRows}");
                        EditorGUILayout.LabelField($"World Rows: {segmentBaseWorldRows[manualSegmentIndex]} → {segmentBaseWorldRows[manualSegmentIndex] + Mathf.Max(0, seg.LengthInRows) - 1}");
                    }
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);

            brushMode = (BrushMode)EditorGUILayout.EnumPopup(
                new GUIContent(gridConfig.Topology == GridTopology.Hex ? "Brush Mode (Hex)" : "Brush Mode", "Single / Line / Rect (Rectangular) or Area (Hex)."),
                brushMode);

            if (gridConfig.Topology == GridTopology.Rectangle && brushMode == BrushMode.Line)
            {
                lineAxis = (LineAxis)EditorGUILayout.EnumPopup(
                    new GUIContent("Line Axis", "Lock line to horizontal or vertical, or auto-align by drag."),
                    lineAxis);
            }

            if (gridConfig.Topology == GridTopology.Hex && brushMode == BrushMode.RectOrArea)
            {
                hexAreaRadius = EditorGUILayout.IntSlider(
                    new GUIContent("Area Radius (Hex)", "In Hex mode, Rect is replaced by disk area."),
                    hexAreaRadius, 0, 48);
                hexLineSnap6Dirs = EditorGUILayout.ToggleLeft(new GUIContent("Snap Line to 6 Directions (Shift to force)"), hexLineSnap6Dirs);
            }

            brushSpawnType = (SpawnType)EditorGUILayout.EnumPopup(
                new GUIContent("Spawn Type", "SpawnType written to each painted entry."),
                brushSpawnType);

            // Enemy-only color controls
            if (brushSpawnType == SpawnType.Enemy)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Enemy Color", EditorStyles.boldLabel);
                enemyPalette = (EnemyColorPalette)EditorGUILayout.ObjectField(
                    new GUIContent("Palette", "Palette used to resolve Enemy hue/tone into a Color for previews and runtime."),
                    enemyPalette, typeof(EnemyColorPalette), false);

                using (new EditorGUI.DisabledScope(enemyPalette == null))
                {
                    brushEnemyHue = (EnemyHue)EditorGUILayout.EnumPopup(new GUIContent("Hue"), brushEnemyHue);
                    brushEnemyTone = (EnemyTone)EditorGUILayout.EnumPopup(new GUIContent("Tone"), brushEnemyTone);
                }
            }

            // Definition picker for Enemy or Boss
            if (brushSpawnType == SpawnType.Enemy || brushSpawnType == SpawnType.Boss)
            {
                EditorGUILayout.Space(4);
                brushEnemyDefinition = (EnemyDefinition)EditorGUILayout.ObjectField(
                    new GUIContent("Enemy Definition", "Stats & scaling SO used by this spawn (Enemy or Boss)."),
                    brushEnemyDefinition, typeof(EnemyDefinition), false);
            }

            // Count + Spacing on one row
            var countRow = EditorGUILayout.GetControlRect();
            int newCount = Mathf.Max(1, EditorGUI.IntField(new Rect(countRow.x, countRow.y, countRow.width * 0.5f - 4, countRow.height),
                new GUIContent("Count (stack/line)", "Number of repeated placements per seed cell (stacks down rows)."), brushCount));
            int newSpacing = Mathf.Max(1, EditorGUI.IntField(new Rect(countRow.x + countRow.width * 0.5f + 4, countRow.y, countRow.width * 0.5f - 4, countRow.height),
                new GUIContent("Spacing (rows)", "Row spacing between repeated placements when Count > 1."), brushSpacingRows));
            brushCount = newCount; brushSpacingRows = newSpacing;

            eraseMode = EditorGUILayout.ToggleLeft(
                new GUIContent("Erase Mode", "When enabled, brush removes matching entries instead of adding."),
                eraseMode);

            rightClickErases = EditorGUILayout.ToggleLeft(
                new GUIContent("Right-Click Erases", "Right-click acts as erase regardless of the Erase Mode toggle."),
                rightClickErases);

            avoidDuplicates = EditorGUILayout.ToggleLeft(
                new GUIContent("Avoid Duplicates", "Skip painting if an entry with same (col,row,type) already exists."),
                avoidDuplicates);

            onConflict = (OnConflictAction)EditorGUILayout.EnumPopup(
                new GUIContent("On-Conflict", "Action when the target cell already contains an entry."),
                onConflict);

            movementDefinition = (MovementDefinition)EditorGUILayout.ObjectField(
                new GUIContent("Movement Payload", "MovementDefinition (or Composite) written to each painted entry.\nLeave empty for none."),
                movementDefinition, typeof(MovementDefinition), false);

            // --- Structured Tags ---
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);

            useStructuredTags = EditorGUILayout.ToggleLeft(
                new GUIContent("Use Structured Tags", "When enabled, tags are built from the controls below (no typos). Turn off to use free-form CSV."),
                useStructuredTags);

            if (useStructuredTags)
            {
                showStructuredTags = EditorGUILayout.Foldout(showStructuredTags, "Structured Tag Controls", true);
                if (showStructuredTags)
                {
                    DrawStructuredTagUI();
                }
            }
            else
            {
                brushTagsCsv = EditorGUILayout.TextField(
                    new GUIContent("Brush Tags (CSV)", "Comma-separated tags to store per entry.\nExamples: 'anchor=RingA, pivot=here'  or  'anchor=RingA, phase=90, r=3.5'"),
                    brushTagsCsv);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Restrictions", EditorStyles.boldLabel);
            restrictToSegmentBounds = EditorGUILayout.ToggleLeft(
                new GUIContent("Restrict to Segment Bounds", "Confine painting during a drag to the active segment's world-row range."),
                restrictToSegmentBounds);

            disallowPaintingInSpacer = EditorGUILayout.ToggleLeft(
                new GUIContent("Disallow Painting in Spacer", "Block painting when SegmentType is Spacer or segment name contains 'Spacer'."),
                disallowPaintingInSpacer);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(gridConfig == null || levelDefinition == null))
            {
                if (GUILayout.Button("Focus Scene on Grid Origin"))
                {
                    if (SceneView.lastActiveSceneView != null)
                    {
                        SceneView.lastActiveSceneView.pivot = GridToWorldCenter(0, 0);
                        SceneView.lastActiveSceneView.Repaint();
                    }
                }

                if (GUILayout.Button("Validate & Fix LevelDefinition"))
                    ValidateAndFixLevel();

                if (GUILayout.Button("Auto-Migrate Misplaced Entries"))
                    AutoMigrateMisplacedEntries();
            }
        }

        DrawStatusBar();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Usage:\n" +
            "• AutoFromClick: click/drag where you see; tool finds the segment automatically and locks the drag to it.\n" +
            "• Brushes: Single / Line (Auto/H/V on Rect; 6-dir on Hex) / Rect (Rect) or Area (Hex); Count & Spacing; Right-click can erase.\n" +
            "• Eyedropper: hover a cell and press 'I' to copy SpawnType + Payload + Tags into the brush.\n" +
            "• On-Conflict: Skip, Overwrite Payload+Tags, or Overwrite All when repainting an existing cell.\n" +
            "• Structured Tags: safer per-payload controls → converts to tags automatically.\n" +
            "• ManualSegment: number keys 1–9 switch segments.\n" +
            "• Validate & Fix keeps entries within bounds and correct segments.",
            MessageType.Info);
    }
    #endregion

    #region Structured Tags UI/Logic (unchanged)
    private void DrawStructuredTagUI()
    {
        if (movementDefinition == null)
        {
            EditorGUILayout.HelpBox("Select a Movement Payload to show relevant structured tag fields.", MessageType.None);
            return;
        }

        var typeName = movementDefinition.GetType().Name;

        // Common: Anchor (used by Anchor + Orbit followers)
        if (typeName == "PivotAnchorDefinition" || typeName == "OrbitAroundAnchorDefinition")
        {
            st_anchorKey = EditorGUILayout.TextField(
                new GUIContent("Anchor Key", "Registry key that binds anchor and its followers (e.g., RingA)."),
                st_anchorKey);
            if (string.IsNullOrWhiteSpace(st_anchorKey)) st_anchorKey = "RingA";
        }

        // Pivot-only fields
        if (typeName == "PivotAnchorDefinition")
        {
            st_pivotMode = (PivotMode)EditorGUILayout.EnumPopup(
                new GUIContent("Pivot Mode", "Where is the pivot placed? None=omit tag, Here=use painted cell, Grid=explicit grid col,row."),
                st_pivotMode);

            using (new EditorGUI.DisabledScope(st_pivotMode != PivotMode.Grid))
            {
                var row = EditorGUILayout.GetControlRect();
                st_pivotCol = EditorGUI.IntField(new Rect(row.x, row.y, row.width * 0.5f - 4, row.height),
                    new GUIContent("Pivot Col", "Grid column for pivot when Pivot Mode = Grid."),
                    st_pivotCol);
                st_pivotRow = EditorGUI.IntField(new Rect(row.x + row.width * 0.5f + 4, row.y, row.width * 0.5f - 4, row.height),
                    new GUIContent("Pivot Row", "Grid row for pivot when Pivot Mode = Grid."),
                    st_pivotRow);
            }
        }

        // Orbit-only fields
        if (typeName == "OrbitAroundAnchorDefinition")
        {
            var row1 = EditorGUILayout.GetControlRect();
            st_phaseDeg = EditorGUI.FloatField(new Rect(row1.x, row1.y, row1.width * 0.5f - 4, row1.height),
                new GUIContent("Phase (deg)", "Per-follower phase offset in degrees (optional)."),
                st_phaseDeg);
            st_radius = EditorGUI.FloatField(new Rect(row1.x + row1.width * 0.5f + 4, row1.y, row1.width * 0.5f - 4, row1.height),
                new GUIContent("Radius", "If > 0, forces radius; otherwise inferred from spawn position."),
                st_radius);
        }

        // Show preview of the composed tag list
        var composed = BuildStructuredTagsPreview(typeName);
        EditorGUILayout.LabelField(new GUIContent("Tags Preview", "This is what will be written to SpawnEntry.tags."), EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(composed.Count > 0 ? string.Join(", ", composed) : "(no tags)", MessageType.None);
    }

    private List<string> BuildStructuredTagsPreview(string payloadTypeName)
    {
        var list = new List<string>();

        if (payloadTypeName == "PivotAnchorDefinition" || payloadTypeName == "OrbitAroundAnchorDefinition")
        {
            if (!string.IsNullOrWhiteSpace(st_anchorKey))
                list.Add($"anchor={st_anchorKey}");
        }

        if (payloadTypeName == "PivotAnchorDefinition")
        {
            if (st_pivotMode == PivotMode.Here)
                list.Add("pivot=here");
            else if (st_pivotMode == PivotMode.Grid)
                list.Add($"pivot={st_pivotCol},{st_pivotRow}");
        }

        if (payloadTypeName == "OrbitAroundAnchorDefinition")
        {
            if (Mathf.Abs(st_phaseDeg) > Mathf.Epsilon) list.Add($"phase={st_phaseDeg}");
            if (st_radius > 0f) list.Add($"r={st_radius}");
        }

        return list;
    }

    private List<string> GetBrushTags()
    {
        if (!useStructuredTags || movementDefinition == null)
            return ParseTags(brushTagsCsv);

        var typeName = movementDefinition.GetType().Name;
        return BuildStructuredTagsPreview(typeName);
    }

    // Populate structured fields after Eyedropper
    private void PopulateStructuredFromTags(List<string> tags, string payloadTypeName)
    {
        if (tags == null) tags = new List<string>(0);

        // reset sensible defaults
        st_anchorKey = "RingA";
        st_pivotMode = PivotMode.None;
        st_pivotCol = 0; st_pivotRow = 0;
        st_phaseDeg = 0f; st_radius = -1f;

        foreach (var raw in tags)
        {
            var s = raw?.Trim(); if (string.IsNullOrEmpty(s)) continue;

            if (s.StartsWith("anchor=", System.StringComparison.OrdinalIgnoreCase))
                st_anchorKey = s.Substring(7).Trim();

            if (payloadTypeName == "PivotAnchorDefinition")
            {
                if (s.Equals("pivot=here", System.StringComparison.OrdinalIgnoreCase))
                    st_pivotMode = PivotMode.Here;
                else if (s.StartsWith("pivot=", System.StringComparison.OrdinalIgnoreCase))
                {
                    var p = s.Substring(6).Trim().Split(',');
                    if (p.Length == 2 && int.TryParse(p[0], out int c) && int.TryParse(p[1], out int r))
                    {
                        st_pivotMode = PivotMode.Grid;
                        st_pivotCol = c; st_pivotRow = r;
                    }
                }
            }

            if (payloadTypeName == "OrbitAroundAnchorDefinition")
            {
                if (s.StartsWith("phase=", System.StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(s.Substring(6).Trim(), out var d)) st_phaseDeg = d;

                if (s.StartsWith("r=", System.StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(s.Substring(2).Trim(), out var rv)) st_radius = rv;
            }
        }
    }
    #endregion

    #region Status
    private void DrawStatusBar()
    {
        if (EditorApplication.timeSinceStartup < statusUntilTime)
            EditorGUILayout.HelpBox(statusMessage, statusType);
    }

    private void ShowTempStatus(string message, MessageType type, double seconds = 1.5)
    {
        statusMessage = message;
        statusType = type;
        statusUntilTime = EditorApplication.timeSinceStartup + seconds;
        Repaint();
    }
    #endregion

    #region Stripe Support (cache + hover resolution)

    /// <summary>World Y step per row for a given config. Use the same values as your gizmo/painter.</summary>
    private float RowStepY(GridConfig cfg)
    {
        switch (cfg.Topology)
        {
            case GridTopology.Rectangle: return cfg.CellHeight;
            case GridTopology.Hex:
                return (cfg.HexOrientation == HexOrientation.PointyTop)
                    ? 1.5f * cfg.HexSize
                    : Mathf.Sqrt(3f) * cfg.HexSize;
            case GridTopology.Octagon: return 2f * cfg.OctApothem;
            default: return 1f;
        }
    }

    /// <summary>Build per-segment stripe maps after segment bases are computed.</summary>
    private void RebuildStripeMaps()
    {
        segmentStripeMaps.Clear();

        if (levelDefinition == null || levelDefinition.Segments == null) return;

        for (int i = 0; i < levelDefinition.Segments.Count; i++)
        {
            var seg = levelDefinition.Segments[i];
            int baseWorld = (i < segmentBaseWorldRows.Count) ? segmentBaseWorldRows[i] : 0;

            var stripes = SegmentStripeMap.Build(
                seg,
                baseWorld,
                gridConfig, // level default config when no stripes are defined
                RowStepY
            );
            segmentStripeMaps.Add(stripes);
        }
    }

    /// <summary>
    /// Stripe-aware hover resolution. Finds (segment, stripe) under the mouse and returns (column, worldRow, localRow).
    /// </summary>
    private bool TryResolveHoverCell_Stripes(Vector3 mouseWorld,
                                             out int column, out int worldRow,
                                             out int segmentIndex, out int segmentLocalRow,
                                             out int stripeIndex)
    {
        column = worldRow = segmentIndex = segmentLocalRow = stripeIndex = -1;

        if (levelDefinition == null || levelDefinition.Segments == null) return false;

        // If we're in Manual mode, search only that segment; otherwise search all segments.
        int startSeg = 0;
        int endSeg = levelDefinition.Segments.Count - 1;
        if (targetMode == TargetMode.ManualSegment)
        {
            startSeg = endSeg = Mathf.Clamp(manualSegmentIndex, 0, levelDefinition.Segments.Count - 1);
        }

        for (int si = startSeg; si <= endSeg; si++)
        {
            if (si < 0 || si >= segmentStripeMaps.Count) continue;
            var stripes = segmentStripeMaps[si];
            if (stripes == null || stripes.Count == 0) continue;

            for (int s = 0; s < stripes.Count; s++)
            {
                var stripe = stripes[s];

                // Convert world->grid using the stripe's topology/config (adapter handles yBase)
                if (!GridStripeAdapter.WorldToGrid(mouseWorld, stripe, out int col, out int rowInStripe))
                    continue;

                // Clamp X to valid columns, and require a non-negative row
                if (col < 0 || col >= stripe.config.Columns || rowInStripe < 0) continue;

                // Compute global indices
                int segLocal = stripe.segmentLocalStartRow + rowInStripe;
                int wRow = stripe.worldStartRow + rowInStripe;

                // Also ensure we don't exceed the stripe's end row
                if (segLocal >= stripe.segmentLocalEndRow || wRow >= stripe.worldEndRow) continue;

                // Success
                column = col;
                worldRow = wRow;
                segmentIndex = si;
                segmentLocalRow = segLocal;
                stripeIndex = s;
                return true;
            }
        }

        return false;
    }

#endregion // Stripe Support

    #region Scene GUI
    private void DuringSceneGUI(SceneView sceneView)
    {
        if (gridConfig == null || levelDefinition == null || levelDefinition.Segments == null || levelDefinition.Segments.Count == 0) return;

        Event evt = Event.current;
        if (evt == null) return;

        // Segment hotkeys in Manual mode
        if (targetMode == TargetMode.ManualSegment && evt.type == EventType.KeyDown)
        {
            int numberKeyIndex = -1;
            if (evt.keyCode >= KeyCode.Alpha1 && evt.keyCode <= KeyCode.Alpha9)
                numberKeyIndex = (int)evt.keyCode - (int)KeyCode.Alpha1;

            if (numberKeyIndex >= 0 && numberKeyIndex < (levelDefinition.Segments?.Count ?? 0))
            {
                manualSegmentIndex = numberKeyIndex;
                Repaint();
                evt.Use();
            }
        }

        // Raycast mouse to Z=0 plane
        if (!TryGetMouseWorldOnZ0(evt.mousePosition, out Vector3 mouseWorld)) return;

        // Stripe-aware: resolve (col, worldRow, segment, localRow) directly from stripes
        if (!TryResolveHoverCell_Stripes(mouseWorld,
                                         out int hoverColumn,
                                         out int hoverWorldRow,
                                         out int segmentIndex,
                                         out int localRow,
                                         out int stripeIndex))
        {
            DrawHoverPanel(-1, -1, -1, -1, "Out of range");
            return;
        }

        var segment = levelDefinition.Segments[segmentIndex];
        DrawHoverPanel(hoverColumn, hoverWorldRow, segmentIndex, localRow, segment != null ? segment.SegmentType.ToString() : "Null");

        // Eyedropper: copy existing cell's settings into the brush (press 'I')
        if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.I)
        {
            var segForPick = levelDefinition.Segments[segmentIndex];
            var picked = FindEntryAt(segForPick, localRow, hoverColumn);
            if (picked != null)
            {
                brushSpawnType = picked.spawnType;
                movementDefinition = picked.payload;

                // Text CSV (always maintained)
                brushTagsCsv = (picked.tags != null && picked.tags.Count > 0) ? string.Join(", ", picked.tags) : "";

                // Structured (if enabled)
                if (useStructuredTags && movementDefinition != null)
                    PopulateStructuredFromTags(picked.tags, movementDefinition.GetType().Name);

                // Copy definition for Enemy OR Boss
                if (picked.spawnType == SpawnType.Enemy || picked.spawnType == SpawnType.Boss)
                {
                    var soSeg = new SerializedObject(levelDefinition.Segments[segmentIndex]);
                    var tProp = soSeg.FindProperty("spawnTable");
                    for (int i = 0; i < tProp.arraySize; i++)
                    {
                        var el = tProp.GetArrayElementAtIndex(i);
                        if (el.FindPropertyRelative("rowOffset").intValue == localRow &&
                            el.FindPropertyRelative("column").intValue == hoverColumn)
                        {
                            var defProp = el.FindPropertyRelative("enemyDefinition");
                            if (defProp != null)
                                brushEnemyDefinition = defProp.objectReferenceValue as EnemyDefinition;
                            break;
                        }
                    }
                }

                // Copy color only for Enemy
                if (picked.spawnType == SpawnType.Enemy)
                {
                    var soSeg = new SerializedObject(levelDefinition.Segments[segmentIndex]);
                    var tProp = soSeg.FindProperty("spawnTable");
                    for (int i = 0; i < tProp.arraySize; i++)
                    {
                        var el = tProp.GetArrayElementAtIndex(i);
                        if (el.FindPropertyRelative("rowOffset").intValue == localRow &&
                            el.FindPropertyRelative("column").intValue == hoverColumn)
                        {
                            var colorProp = el.FindPropertyRelative("enemyColor");
                            if (colorProp != null)
                            {
                                brushEnemyHue = (EnemyHue)colorProp.FindPropertyRelative("hue").enumValueIndex;
                                brushEnemyTone = (EnemyTone)colorProp.FindPropertyRelative("tone").enumValueIndex;
                            }
                            break;
                        }
                    }
                }


                ShowTempStatus($"Eyedropper: loaded type={brushSpawnType}, payload={(movementDefinition ? movementDefinition.name : "None")}, tags=({brushTagsCsv})", MessageType.Info, 1.0);
                Repaint();
            }
            evt.Use();
        }

        // Mouse input
        if (evt.type == EventType.MouseDown && (evt.button == 0 || evt.button == 1) && !evt.alt)
        {
            if (!TryResolveSegmentAndLocalRow(hoverWorldRow, out int startSegIndex, out int startLocalRow)) { evt.Use(); return; }
            if (!IsPaintAllowedInSegment(startSegIndex)) { ShowTempStatus("Painting disallowed in this segment.", MessageType.Warning); evt.Use(); return; }

            isDragging = true;
            dragStartCell = new Vector2Int(hoverColumn, hoverWorldRow);
            dragEndCell = dragStartCell;
            dragSegmentIndex = startSegIndex;
            dragLocalRowStart = startLocalRow;
            dragSegmentName = levelDefinition.Segments[startSegIndex] != null ? levelDefinition.Segments[startSegIndex].SegmentType.ToString() : "Null";
            evt.Use();
        }
        else if (evt.type == EventType.MouseDrag && isDragging)
        {
            dragEndCell = new Vector2Int(hoverColumn, hoverWorldRow);
            evt.Use();
        }
        else if (evt.type == EventType.MouseUp && isDragging)
        {
            bool doErase = eraseMode || (rightClickErases && evt.button == 1);
            ApplyBrush_Stripes(dragStartCell, dragEndCell, dragSegmentIndex, doErase);
            isDragging = false;
            dragSegmentIndex = -1;
            evt.Use();
        }

        // Visual aids
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        DrawHoverHighlight_Stripes(hoverColumn, hoverWorldRow, segmentIndex, localRow, stripeIndex);

        if (isDragging) DrawDragPreview_Stripes();
    }

    private static bool TryGetMouseWorldOnZ0(Vector2 guiPosition, out Vector3 world)
    {
        var ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        if (Mathf.Approximately(ray.direction.z, 0f)) { world = default; return false; }
        float t = -ray.origin.z / ray.direction.z;
        world = ray.origin + ray.direction * t;
        return true;
    }
    #endregion

#if UNITY_EDITOR
    // -----------------------------------------------------------------------------
    // HOVER HIGHLIGHT (stripe-aware)
    // Draws the hovered cell outline using the active stripe's topology & config.
    // -----------------------------------------------------------------------------
    private void DrawHoverHighlight_Stripes(int hoverColumn, int hoverWorldRow, int segmentIndex, int segmentLocalRow, int stripeIndex)
    {
        if (levelDefinition == null || gridConfig == null) return;
        if (segmentIndex < 0 || segmentIndex >= levelDefinition.Segments.Count) return;
        if (segmentIndex >= segmentStripeMaps.Count) return;

        var stripes = segmentStripeMaps[segmentIndex];
        if (stripes == null || stripeIndex < 0 || stripeIndex >= stripes.Count) return;

        var s = stripes[stripeIndex];

        // stripe-local row
        int rowInStripe = segmentLocalRow - s.segmentLocalStartRow;
        if (rowInStripe < 0 || rowInStripe >= (s.segmentLocalEndRow - s.segmentLocalStartRow)) return;

        // world center for this cell (already includes hex centering and stripe yBase via adapter)
        var center = GridStripeAdapter.GridToWorld(hoverColumn, rowInStripe, s);

        UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.9f);
        UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        switch (s.topology)
        {
            case GridTopology.Rectangle:
                {
                    float hw = s.config.CellWidth * 0.5f;
                    float hh = s.config.CellHeight * 0.5f;
                    Vector3 p0 = center + new Vector3(-hw, -hh, 0f);
                    Vector3 p1 = center + new Vector3(hw, -hh, 0f);
                    Vector3 p2 = center + new Vector3(hw, hh, 0f);
                    Vector3 p3 = center + new Vector3(-hw, hh, 0f);
                    UnityEditor.Handles.DrawAAPolyLine(3f, p0, p1, p2, p3, p0);
                    break;
                }

            case GridTopology.Hex:
                {
                    // Draw a hex around 'center' using your HexSize and orientation.
                    float R = s.config.HexSize;
                    Vector3[] v = new Vector3[7];

                    if (s.config.HexOrientation == HexOrientation.PointyTop)
                    {
                        // Pointy: 0° is to the right, rotate -30° so a point faces up.
                        for (int i = 0; i < 6; i++)
                        {
                            float ang = Mathf.Deg2Rad * (60f * i - 30f);
                            v[i] = center + new Vector3(Mathf.Cos(ang) * R, Mathf.Sin(ang) * R, 0f);
                        }
                    }
                    else // FlatTop
                    {
                        // Flat: 0° is to the right, a flat side is up.
                        for (int i = 0; i < 6; i++)
                        {
                            float ang = Mathf.Deg2Rad * (60f * i);
                            v[i] = center + new Vector3(Mathf.Cos(ang) * R, Mathf.Sin(ang) * R, 0f);
                        }
                    }

                    v[6] = v[0];
                    UnityEditor.Handles.DrawAAPolyLine(3f, v);
                    break;
                }

            case GridTopology.Octagon:
                {
                    // Regular octagon using apothem
                    float a = s.config.OctApothem;
                    float R = a / Mathf.Cos(Mathf.PI / 8f); // circumradius
                    Vector3[] v = new Vector3[9];
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = Mathf.Deg2Rad * (45f * i + 22.5f); // flat-ish top
                        v[i] = center + new Vector3(Mathf.Cos(ang) * R, Mathf.Sin(ang) * R, 0f);
                    }
                    v[8] = v[0];
                    UnityEditor.Handles.DrawAAPolyLine(3f, v);
                    break;
                }
        }
    }
#endif

#if UNITY_EDITOR
    // -----------------------------------------------------------------------------
    // DRAG PREVIEW (stripe-aware, uses your fields)
    // Splits preview across vertical stripe slices; supports Hex line/area when
    // the operation stays inside a single hex stripe.
    // -----------------------------------------------------------------------------
    private void DrawDragPreview_Stripes()
    {
        if (levelDefinition == null || gridConfig == null) return;
        if (!isDragging) return;

        int segIndex = dragSegmentIndex;
        if (segIndex < 0 || segIndex >= levelDefinition.Segments.Count) return;
        if (segIndex >= segmentStripeMaps.Count) return;

        var stripes = segmentStripeMaps[segIndex];
        if (stripes == null || stripes.Count == 0) return;

        // Segment row bounds
        int segBase = segmentBaseWorldRows[segIndex];
        int segLen = Mathf.Max(0, levelDefinition.Segments[segIndex]?.LengthInRows ?? 0);
        int segEndWorld = segBase + segLen - 1;

        // World-space start/end (as you store them)
        var startCell = dragStartCell;
        var endCell = dragEndCell;

        // Clamp to segment bounds for visuals (respect your toggle)
        if (restrictToSegmentBounds)
        {
            startCell.y = Mathf.Clamp(startCell.y, segBase, segEndWorld);
            endCell.y = Mathf.Clamp(endCell.y, segBase, segEndWorld);
        }

        // Convert to segment-local rows for slicing
        int startLocal = Mathf.Clamp(startCell.y - segBase, 0, Mathf.Max(0, segLen - 1));
        int endLocal = Mathf.Clamp(endCell.y - segBase, 0, Mathf.Max(0, segLen - 1));
        int segStartRow = Mathf.Min(startLocal, endLocal);
        int segEndRowEx = Mathf.Max(startLocal, endLocal) + 1;

        int startCol = startCell.x;
        int endCol = endCell.x;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        switch (brushMode)
        {
            // -------------------------------------------------------------
            case BrushMode.Single:
                {
                    // Find stripe that contains the startLocal row
                    foreach (var s in stripes)
                    {
                        if (startLocal >= s.segmentLocalStartRow && startLocal < s.segmentLocalEndRow)
                        {
                            int rowInStripe = startLocal - s.segmentLocalStartRow;
                            int colInStripe = Mathf.Clamp(startCol, 0, s.config.Columns - 1);
                            var c = GridStripeAdapter.GridToWorld(colInStripe, rowInStripe, s);
                            DrawCellOutlineForStripe(s, c);
                            break;
                        }
                    }
                    break;
                }

            // -------------------------------------------------------------
            case BrushMode.Line:
                {
                    // Respect your H/V lock if set
                    if (lineAxis == LineAxis.Horizontal) endCol = startCol;
                    else if (lineAxis == LineAxis.Vertical) endCell.y = startCell.y; // recomputed below

                    bool vertical = (startCol == endCol);
                    bool horizontal = (startCell.y == endCell.y);

                    if (vertical)
                    {
                        // Fixed column, varying rows → split by stripe slices
                        foreach (var slice in EnumerateStripeSlices(segIndex, segStartRow, segEndRowEx))
                        {
                            var s = slice.stripe;
                            int col = Mathf.Clamp(startCol, 0, s.config.Columns - 1);

                            for (int r = slice.stripeStartRow; r < slice.stripeEndRowExclusive; r++)
                            {
                                var c = GridStripeAdapter.GridToWorld(col, r, s);
                                DrawCellOutlineForStripe(s, c);
                            }
                        }
                    }
                    else if (horizontal)
                    {
                        // Fixed row, varying columns (one stripe holds that row)
                        foreach (var s in stripes)
                        {
                            if (startLocal < s.segmentLocalStartRow || startLocal >= s.segmentLocalEndRow) continue;
                            int r = startLocal - s.segmentLocalStartRow;

                            int colA = Mathf.Min(startCol, endCol);
                            int colB = Mathf.Max(startCol, endCol);
                            colA = Mathf.Clamp(colA, 0, s.config.Columns - 1);
                            colB = Mathf.Clamp(colB, 0, s.config.Columns - 1);

                            for (int cCol = colA; cCol <= colB; cCol++)
                            {
                                var c = GridStripeAdapter.GridToWorld(cCol, r, s);
                                DrawCellOutlineForStripe(s, c);
                            }
                        }
                    }
                    else
                    {
                        // Diagonal/oblique drag.
                        // If BOTH endpoints are inside the SAME HEX stripe, show a true axial hex line.
                        for (int i = 0; i < stripes.Count; i++)
                        {
                            var s = stripes[i];
                            bool startIn = (startLocal >= s.segmentLocalStartRow && startLocal < s.segmentLocalEndRow);
                            bool endIn = (endLocal >= s.segmentLocalStartRow && endLocal < s.segmentLocalEndRow);

                            if (!startIn || !endIn) continue;
                            if (s.topology != GridTopology.Hex) continue;

                            int r0 = startLocal - s.segmentLocalStartRow;
                            int r1 = endLocal - s.segmentLocalStartRow;

                            // Offset -> Axial using THIS stripe's config (your API)
                            var a0 = HexGridMath.OffsetToAxial(startCol, r0, s.config.HexOrientation, s.config.HexOffset);
                            var a1 = HexGridMath.OffsetToAxial(endCol, r1, s.config.HexOrientation, s.config.HexOffset);

                            foreach (var a in HexGridMath.HexLine(a0, a1))
                            {
                                HexGridMath.AxialToOffset(a, s.config.HexOrientation, s.config.HexOffset, out int c, out int r);
                                if (c >= 0 && c < s.config.Columns &&
                                    r >= 0 && r < (s.segmentLocalEndRow - s.segmentLocalStartRow))
                                {
                                    var worldC = GridStripeAdapter.GridToWorld(c, r, s);
                                    DrawCellOutlineForStripe(s, worldC);
                                }
                            }
                            break; // only one stripe can satisfy this
                        }
                    }
                    break;
                }

            // -------------------------------------------------------------
            case BrushMode.RectOrArea:
                {
                    // If the start point is inside a HEX stripe, draw a disk Area preview there.
                    bool handledHexArea = false;
                    foreach (var s in stripes)
                    {
                        if (s.topology != GridTopology.Hex) continue;
                        if (startLocal < s.segmentLocalStartRow || startLocal >= s.segmentLocalEndRow) continue;

                        int r0 = startLocal - s.segmentLocalStartRow;

                        var centerAx = HexGridMath.OffsetToAxial(startCol, r0, s.config.HexOrientation, s.config.HexOffset);
                        foreach (var a in HexGridMath.HexDisk(centerAx, Mathf.Max(0, hexAreaRadius)))
                        {
                            HexGridMath.AxialToOffset(a, s.config.HexOrientation, s.config.HexOffset, out int c, out int r);
                            if (c >= 0 && c < s.config.Columns &&
                                r >= 0 && r < (s.segmentLocalEndRow - s.segmentLocalStartRow))
                            {
                                var worldC = GridStripeAdapter.GridToWorld(c, r, s);
                                DrawCellOutlineForStripe(s, worldC);
                            }
                        }
                        handledHexArea = true;
                        break;
                    }

                    if (handledHexArea) break;

                    // Otherwise: rectangular box selection, split by stripe slices
                    int colA = Mathf.Min(startCol, endCol);
                    int colB = Mathf.Max(startCol, endCol);

                    foreach (var slice in EnumerateStripeSlices(segIndex, segStartRow, segEndRowEx))
                    {
                        var s = slice.stripe;
                        int minCol = Mathf.Clamp(colA, 0, s.config.Columns - 1);
                        int maxCol = Mathf.Clamp(colB, 0, s.config.Columns - 1);

                        for (int r = slice.stripeStartRow; r < slice.stripeEndRowExclusive; r++)
                        {
                            for (int cCol = minCol; cCol <= maxCol; cCol++)
                            {
                                var c = GridStripeAdapter.GridToWorld(cCol, r, s);
                                DrawCellOutlineForStripe(s, c);
                            }
                        }
                    }
                    break;
                }
        }
    }
#endif


#if UNITY_EDITOR
    // -----------------------------------------------------------------------------
    // Helper: draw one cell outline for a given stripe + world center
    // Uses your GridConfig names: CellWidth/CellHeight/HexSize/OctApothem
    // -----------------------------------------------------------------------------
    private void DrawCellOutlineForStripe(StripeInfo s, Vector3 center)
    {
        UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.6f);

        switch (s.topology)
        {
            case GridTopology.Rectangle:
                {
                    float hw = s.config.CellWidth * 0.5f;
                    float hh = s.config.CellHeight * 0.5f;
                    Vector3 p0 = center + new Vector3(-hw, -hh, 0f);
                    Vector3 p1 = center + new Vector3(hw, -hh, 0f);
                    Vector3 p2 = center + new Vector3(hw, hh, 0f);
                    Vector3 p3 = center + new Vector3(-hw, hh, 0f);
                    UnityEditor.Handles.DrawAAPolyLine(2f, p0, p1, p2, p3, p0);
                    break;
                }

            case GridTopology.Hex:
                {
                    float R = s.config.HexSize;
                    Vector3[] v = new Vector3[7];

                    if (s.config.HexOrientation == HexOrientation.PointyTop)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            float ang = Mathf.Deg2Rad * (60f * i - 30f);
                            v[i] = center + new Vector3(Mathf.Cos(ang) * R, Mathf.Sin(ang) * R, 0f);
                        }
                    }
                    else // FlatTop
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            float ang = Mathf.Deg2Rad * (60f * i);
                            v[i] = center + new Vector3(Mathf.Cos(ang) * R, Mathf.Sin(ang) * R, 0f);
                        }
                    }

                    v[6] = v[0];
                    UnityEditor.Handles.DrawAAPolyLine(2f, v);
                    break;
                }

            case GridTopology.Octagon:
                {
                    float a = s.config.OctApothem;
                    float R = a / Mathf.Cos(Mathf.PI / 8f);
                    Vector3[] v = new Vector3[9];
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = Mathf.Deg2Rad * (45f * i + 22.5f);
                        v[i] = center + new Vector3(Mathf.Cos(ang) * R, Mathf.Sin(ang) * R, 0f);
                    }
                    v[8] = v[0];
                    UnityEditor.Handles.DrawAAPolyLine(2f, v);
                    break;
                }
        }
    }
#endif

    // -----------------------------------------------------------------------------
    // If you don't already have this from Step 1, include it here.
    // Splits a segment-local row interval into per-stripe slices.
    // -----------------------------------------------------------------------------
    private IEnumerable<(StripeInfo stripe, int stripeStartRow, int stripeEndRowExclusive)>
    EnumerateStripeSlices(int segmentIndex, int segStartRow, int segEndRowExclusive)
    {
        if (segmentIndex < 0 || segmentIndex >= segmentStripeMaps.Count)
            yield break;

        var stripes = segmentStripeMaps[segmentIndex];

        foreach (var s in stripes)
        {
            int sStart = s.segmentLocalStartRow;
            int sEnd = s.segmentLocalEndRow;

            int isectStart = Mathf.Max(segStartRow, sStart);
            int isectEnd = Mathf.Min(segEndRowExclusive, sEnd);

            if (isectStart < isectEnd)
            {
                yield return (s, isectStart - sStart, isectEnd - sStart);
            }
        }
    }

    #region Hover/Preview drawing
    private void DrawHoverPanel(int worldCol, int worldRow, int segIndex, int localRow, string segName)
    {
        Handles.BeginGUI();
        Vector2 mouse = Event.current.mousePosition;
        Rect rect = new(mouse.x + 16, mouse.y + 12, 360, 56);
        GUILayout.BeginArea(rect, GUI.skin.box);
        if (segIndex >= 0)
            GUILayout.Label($"World Row: {worldRow} | Col: {worldCol}\nSegment[{segIndex}] {segName} | Local Row: {localRow}");
        else
            GUILayout.Label($"World Row: {worldRow} | Col: {worldCol}\n(Outside any segment)");
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    #endregion

    #region Segment Layout Helpers
    private void RecomputeSegmentBases()
    {
        segmentBaseWorldRows.Clear();
        totalWorldRows = 0;
        if (levelDefinition == null || levelDefinition.Segments == null) return;

        foreach (var seg in levelDefinition.Segments)
        {
            segmentBaseWorldRows.Add(totalWorldRows);
            totalWorldRows += seg ? Mathf.Max(0, seg.LengthInRows) : 0;
        }
    }

    private bool TryResolveSegmentAndLocalRow(int worldRow, out int segIndex, out int localRow)
    {
        segIndex = -1;
        localRow = -1;

        int segCount = levelDefinition?.Segments?.Count ?? 0;
        if (segCount == 0) return false;

        if (targetMode == TargetMode.ManualSegment)
        {
            segIndex = Mathf.Clamp(manualSegmentIndex, 0, segCount - 1);
            int start = segmentBaseWorldRows[segIndex];
            int len = Mathf.Max(0, levelDefinition.Segments[segIndex]?.LengthInRows ?? 0);
            localRow = worldRow - start;
            return (localRow >= 0 && localRow < len);
        }

        // AutoFromClick: find segment containing worldRow
        for (int i = 0; i < segCount; i++)
        {
            int start = segmentBaseWorldRows[i];
            int len = Mathf.Max(0, levelDefinition.Segments[i]?.LengthInRows ?? 0);
            int end = start + len;
            if (worldRow >= start && worldRow < end)
            {
                segIndex = i;
                localRow = worldRow - start;
                return true;
            }
        }
        return false;
    }

    private bool IsPaintAllowedInSegment(int segIndex)
    {
        if (segIndex < 0 || segIndex >= (levelDefinition?.Segments?.Count ?? 0)) return false;
        var seg = levelDefinition.Segments[segIndex];
        if (seg == null) return false;

        if (disallowPaintingInSpacer &&
            (seg.SegmentType == SegmentType.Space || seg.name.ToLower().Contains("spacer")))
            return false;

        return true;
    }
    #endregion

    #region Apply Brush (Rectangle + Hex)
  
    // -----------------------------------------------------------------------------
    // APPLY (stripe-aware): computes targets per stripe, then reuses existing
    // PaintCells/EraseCells to write into the segment.
    // -----------------------------------------------------------------------------
    private void ApplyBrush_Stripes(Vector2Int startCell, Vector2Int endCell, int segIndex, bool erase)
    {
        if (segIndex < 0 || segIndex >= (levelDefinition?.Segments?.Count ?? 0)) return;
        var seg = levelDefinition.Segments[segIndex];
        if (!seg) return;

        // Segment bounds (world-row)
        int segStartWorld = segmentBaseWorldRows[segIndex];
        int segLen = Mathf.Max(0, seg.LengthInRows);
        int segEndWorld = segStartWorld + segLen - 1;

        // Respect restriction (same as your old ApplyBrush)
        if (restrictToSegmentBounds)
        {
            startCell.y = Mathf.Clamp(startCell.y, segStartWorld, segEndWorld);
            endCell.y = Mathf.Clamp(endCell.y, segStartWorld, segEndWorld);
        }

        // Compute all target world cells (col, worldRow) across stripes
        var targets = CollectTargetCells_Stripes(startCell, endCell, segIndex, segStartWorld, segEndWorld);

        if (!IsPaintAllowedInSegment(segIndex))
        {
            ShowTempStatus("Painting disallowed in this segment.", MessageType.Warning);
            return;
        }

        if (erase) EraseCells(seg, targets, segStartWorld);
        else PaintCells(seg, targets, segStartWorld);
    }

    // -----------------------------------------------------------------------------
    // Collect targets across stripes (returns world (col, worldRow) cells)
    // Mirrors DrawDragPreview_Stripes, but produces concrete cells.
    // -----------------------------------------------------------------------------
    private List<Vector2Int> CollectTargetCells_Stripes(
        Vector2Int startCell, Vector2Int endCell,
        int segIndex, int segStartWorld, int segEndWorld)
    {
        var result = new List<Vector2Int>();
        if (segIndex < 0 || segIndex >= segmentStripeMaps.Count) return result;

        var stripes = segmentStripeMaps[segIndex];
        if (stripes == null || stripes.Count == 0) return result;

        // Convert world rows → segment-local rows
        int startLocal = Mathf.Clamp(startCell.y - segStartWorld, 0, Mathf.Max(0, (segEndWorld - segStartWorld)));
        int endLocal = Mathf.Clamp(endCell.y - segStartWorld, 0, Mathf.Max(0, (segEndWorld - segStartWorld)));

        int segStartRow = Mathf.Min(startLocal, endLocal);
        int segEndRowEx = Mathf.Max(startLocal, endLocal) + 1;

        int startCol = startCell.x;
        int endCol = endCell.x;

        switch (brushMode)
        {
            // ---------------------------------------------------------
            case BrushMode.Single:
                {
                    foreach (var s in stripes)
                    {
                        if (startLocal >= s.segmentLocalStartRow && startLocal < s.segmentLocalEndRow)
                        {
                            int rowInStripe = startLocal - s.segmentLocalStartRow;
                            int colInStripe = Mathf.Clamp(startCol, 0, s.config.Columns - 1);
                            int worldRow = s.worldStartRow + rowInStripe;
                            result.Add(new Vector2Int(colInStripe, worldRow));
                            break;
                        }
                    }
                    break;
                }

            // ---------------------------------------------------------
            case BrushMode.Line:
                {
                    // Respect your H/V lock
                    if (lineAxis == LineAxis.Horizontal) endCol = startCol;
                    else if (lineAxis == LineAxis.Vertical) endLocal = startLocal; // fixed row

                    bool vertical = (startCol == endCol);
                    bool horizontal = (startLocal == endLocal);

                    if (vertical)
                    {
                        // Fixed column, varying rows — split per stripe slice
                        foreach (var slice in EnumerateStripeSlices(segIndex, segStartRow, segEndRowEx))
                        {
                            var s = slice.stripe;
                            int col = Mathf.Clamp(startCol, 0, s.config.Columns - 1);

                            for (int r = slice.stripeStartRow; r < slice.stripeEndRowExclusive; r++)
                            {
                                int worldRow = s.worldStartRow + r;
                                result.Add(new Vector2Int(col, worldRow));
                            }
                        }
                    }
                    else if (horizontal)
                    {
                        // Fixed row, varying columns — only one stripe holds that row
                        foreach (var s in stripes)
                        {
                            if (startLocal < s.segmentLocalStartRow || startLocal >= s.segmentLocalEndRow) continue;
                            int rLocal = startLocal - s.segmentLocalStartRow;

                            int colA = Mathf.Min(startCol, endCol);
                            int colB = Mathf.Max(startCol, endCol);
                            colA = Mathf.Clamp(colA, 0, s.config.Columns - 1);
                            colB = Mathf.Clamp(colB, 0, s.config.Columns - 1);

                            for (int c = colA; c <= colB; c++)
                            {
                                int worldRow = s.worldStartRow + rLocal;
                                result.Add(new Vector2Int(c, worldRow));
                            }
                        }
                    }
                    else
                    {
                        // Diagonal/oblique — if both endpoints inside the SAME HEX stripe, do a true hex line
                        for (int i = 0; i < stripes.Count; i++)
                        {
                            var s = stripes[i];
                            bool startIn = (startLocal >= s.segmentLocalStartRow && startLocal < s.segmentLocalEndRow);
                            bool endIn = (endLocal >= s.segmentLocalStartRow && endLocal < s.segmentLocalEndRow);
                            if (!startIn || !endIn) continue;
                            if (s.topology != GridTopology.Hex) continue;

                            int r0 = startLocal - s.segmentLocalStartRow;
                            int r1 = endLocal - s.segmentLocalStartRow;

                            // Offset -> Axial (your API)
                            var a0 = HexGridMath.OffsetToAxial(startCol, r0, s.config.HexOrientation, s.config.HexOffset);
                            var a1 = HexGridMath.OffsetToAxial(endCol, r1, s.config.HexOrientation, s.config.HexOffset);

                            foreach (var a in HexGridMath.HexLine(a0, a1))
                            {
                                HexGridMath.AxialToOffset(a, s.config.HexOrientation, s.config.HexOffset, out int c, out int r);
                                if (c >= 0 && c < s.config.Columns &&
                                    r >= 0 && r < (s.segmentLocalEndRow - s.segmentLocalStartRow))
                                {
                                    int worldRow = s.worldStartRow + r;
                                    result.Add(new Vector2Int(c, worldRow));
                                }
                            }
                            break; // only one stripe can satisfy this
                        }
                    }
                    break;
                }

            // ---------------------------------------------------------
            case BrushMode.RectOrArea:
                {
                    // If starting inside a HEX stripe, treat as Area disk around the start
                    bool handledHexArea = false;
                    foreach (var s in stripes)
                    {
                        if (s.topology != GridTopology.Hex) continue;
                        if (startLocal < s.segmentLocalStartRow || startLocal >= s.segmentLocalEndRow) continue;

                        int r0 = startLocal - s.segmentLocalStartRow;
                        var centerAx = HexGridMath.OffsetToAxial(startCol, r0, s.config.HexOrientation, s.config.HexOffset);

                        foreach (var a in HexGridMath.HexDisk(centerAx, Mathf.Max(0, hexAreaRadius)))
                        {
                            HexGridMath.AxialToOffset(a, s.config.HexOrientation, s.config.HexOffset, out int c, out int r);
                            if (c >= 0 && c < s.config.Columns &&
                                r >= 0 && r < (s.segmentLocalEndRow - s.segmentLocalStartRow))
                            {
                                int worldRow = s.worldStartRow + r;
                                result.Add(new Vector2Int(c, worldRow));
                            }
                        }
                        handledHexArea = true;
                        break;
                    }

                    if (handledHexArea) break;

                    // Otherwise: rectangular box selection across stripes
                    int colA = Mathf.Min(startCol, endCol);
                    int colB = Mathf.Max(startCol, endCol);

                    foreach (var slice in EnumerateStripeSlices(segIndex, segStartRow, segEndRowEx))
                    {
                        var s = slice.stripe;
                        int minCol = Mathf.Clamp(colA, 0, s.config.Columns - 1);
                        int maxCol = Mathf.Clamp(colB, 0, s.config.Columns - 1);

                        for (int r = slice.stripeStartRow; r < slice.stripeEndRowExclusive; r++)
                        {
                            int worldRow = s.worldStartRow + r;
                            for (int c = minCol; c <= maxCol; c++)
                                result.Add(new Vector2Int(c, worldRow));
                        }
                    }
                    break;
                }
        }

        // ---------------------------------------------------------
        // Add stacks (Count/Spacing) for Single/Line, like your original code.
        if (brushMode != BrushMode.RectOrArea && brushCount > 1)
        {
            var extras = new List<Vector2Int>(result.Count * (brushCount - 1));
            int step = Mathf.Max(1, brushSpacingRows);

            foreach (var cell in result)
            {
                for (int i = 1; i < brushCount; i++)
                {
                    int rr = cell.y + i * step; // move down world rows
                    if (rr >= segStartWorld && rr <= segEndWorld)
                        extras.Add(new Vector2Int(cell.x, rr));
                }
            }
            result.AddRange(extras);
        }

        // Final segment-bounds filter (safety)
        if (restrictToSegmentBounds)
        {
            for (int i = result.Count - 1; i >= 0; i--)
                if (result[i].y < segStartWorld || result[i].y > segEndWorld) result.RemoveAt(i);
        }

        return result;
    }

    #endregion

    #region Rectangle/Hex conversions

    private Vector3 GridToWorldCenter(int col, int worldRow)
    {
        if (gridConfig.Topology == GridTopology.Rectangle)
            return GridMath.GridToWorld(col, worldRow, gridConfig) + new Vector3(0f, gridConfig.OriginY, 0f);
        if (gridConfig.Topology == GridTopology.Hex)
            return HexGridCenter(col, worldRow) + new Vector3(0f, gridConfig.OriginY, 0f);

        // Octagon
        return OctGridCenter(col, worldRow) + new Vector3(0f, gridConfig.OriginY, 0f);
    }


    // Hex helpers
    private HexGridMath.Axial HexOffsetToAxial(int col, int row)
        => HexGridMath.OffsetToAxial(col, row, gridConfig.HexOrientation, gridConfig.HexOffset);

    private void AxialToHexOffset(HexGridMath.Axial a, out int col, out int row)
        => HexGridMath.AxialToOffset(a, gridConfig.HexOrientation, gridConfig.HexOffset, out col, out row);

    private Vector3 HexGridCenter(int col, int worldRow)
    {
        var axial = HexOffsetToAxial(col, worldRow);
        var c2 = HexGridMath.AxialToWorld(axial, gridConfig.HexSize, gridConfig.HexOrientation);

        // Horizontal centering similar to rectangle layout
        float colStep = (gridConfig.HexOrientation == HexOrientation.PointyTop)
            ? Mathf.Sqrt(3f) * gridConfig.HexSize
            : 1.5f * gridConfig.HexSize;
        float totalWidth = (gridConfig.Columns - 1) * colStep;
        c2.x -= totalWidth * 0.5f;

        return new Vector3(c2.x, c2.y, 0f);
    }

    private Vector3 OctGridCenter(int col, int worldRow)
    {
        return OctGridMath.GridToWorld(col, worldRow, gridConfig);
    }

    private Vector3[] HexVerts(Vector3 center)
    {
        var v = new Vector3[6];
        GetHexVerts(center, v);
        return v;
    }

    private void GetHexVerts(Vector3 center, Vector3[] outVerts)
    {
        var c2 = new Vector2(center.x, center.y);
        HexGridMath.GetHexCorners(c2, gridConfig.HexSize, gridConfig.HexOrientation, outVerts);
    }
    #endregion

    #region Paint/Erase (unchanged core)
    private void PaintCells(LevelSegment segment, List<Vector2Int> worldCells, int segStart)
    {
        Undo.RecordObject(segment, "Paint Cells");

        var soSegment = new SerializedObject(segment);
        var tableProp = soSegment.FindProperty("spawnTable");

        // Build tags (structured or CSV)
        var parsedTags = GetBrushTags();

        foreach (var worldCell in worldCells)
        {
            int column = Mathf.Max(0, worldCell.x);
            int localRow = Mathf.Max(0, worldCell.y - segStart);

            // If there's already an entry in this cell, obey the On-Conflict policy
            var existing = FindEntryAt(segment, localRow, column);
            if (existing != null)
            {
                if (onConflict == OnConflictAction.Skip)
                {
                    continue;
                }
                else
                {
                    // Edit existing entry in-place
                    var soExisting = new SerializedObject(segment);
                    var tProp = soExisting.FindProperty("spawnTable");

                    int foundIndex = -1;
                    for (int i = 0; i < tProp.arraySize; i++)
                    {
                        var el = tProp.GetArrayElementAtIndex(i);
                        int r = el.FindPropertyRelative("rowOffset").intValue;
                        int c = el.FindPropertyRelative("column").intValue;
                        if (r == localRow && c == column) { foundIndex = i; break; }
                    }

                    if (foundIndex >= 0)
                    {
                        var el = tProp.GetArrayElementAtIndex(foundIndex);

                        if (onConflict == OnConflictAction.OverwriteAll)
                            el.FindPropertyRelative("spawnType").intValue = (int)brushSpawnType;

                        el.FindPropertyRelative("payload").objectReferenceValue = movementDefinition;

                        // Replace tags
                        var tagsProp = el.FindPropertyRelative("tags");
                        tagsProp.arraySize = parsedTags.Count;
                        for (int t = 0; t < parsedTags.Count; t++)
                            tagsProp.GetArrayElementAtIndex(t).stringValue = parsedTags[t];

                        // Normalize default fields
                        el.FindPropertyRelative("count").intValue = 1;
                        el.FindPropertyRelative("spacing").intValue = 1;

                        // Enemy color overwrite rules:
                        // If resulting type is Enemy, write hue/tone from current brush.
                        var finalType = (SpawnType)el.FindPropertyRelative("spawnType").intValue;
                        if (finalType == SpawnType.Enemy || finalType == SpawnType.Boss)
                        {
                            var defProp = el.FindPropertyRelative("enemyDefinition");
                            if (defProp != null) defProp.objectReferenceValue = brushEnemyDefinition;
                        }

                        if (finalType == SpawnType.Enemy)
                        {
                            var colorProp = el.FindPropertyRelative("enemyColor");
                            if (colorProp != null)
                            {
                                colorProp.FindPropertyRelative("hue").enumValueIndex = (int)brushEnemyHue;
                                colorProp.FindPropertyRelative("tone").enumValueIndex = (int)brushEnemyTone;
                            }
                        }

                        soExisting.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(segment);
                    }
                    continue;
                }
            }

            // No existing entry — create a new one
            if (avoidDuplicates && HasEntry(tableProp, column, localRow, brushSpawnType))
                continue;

            int index = tableProp.arraySize;
            tableProp.InsertArrayElementAtIndex(index);
            var entryProp = tableProp.GetArrayElementAtIndex(index);

            entryProp.FindPropertyRelative("rowOffset").intValue = localRow;
            entryProp.FindPropertyRelative("column").intValue = column;
            entryProp.FindPropertyRelative("spawnType").intValue = (int)brushSpawnType;
            entryProp.FindPropertyRelative("payload").objectReferenceValue = movementDefinition;
            entryProp.FindPropertyRelative("count").intValue = 1;
            entryProp.FindPropertyRelative("spacing").intValue = 1;

            // Enemy color (only when type == Enemy)
            if (brushSpawnType == SpawnType.Enemy)
            {
                var colorProp = entryProp.FindPropertyRelative("enemyColor");
                if (colorProp != null)
                {
                    colorProp.FindPropertyRelative("hue").enumValueIndex = (int)brushEnemyHue;
                    colorProp.FindPropertyRelative("tone").enumValueIndex = (int)brushEnemyTone;
                }
            }
            if (brushSpawnType == SpawnType.Enemy || brushSpawnType == SpawnType.Boss)
            {
                var defProp = entryProp.FindPropertyRelative("enemyDefinition");
                if (defProp != null) defProp.objectReferenceValue = brushEnemyDefinition;
            }

            // Write tags
            var tagsPropNew = entryProp.FindPropertyRelative("tags");
            tagsPropNew.arraySize = parsedTags.Count;
            for (int i = 0; i < parsedTags.Count; i++)
                tagsPropNew.GetArrayElementAtIndex(i).stringValue = parsedTags[i];
        }

        soSegment.ApplyModifiedProperties();
        EditorUtility.SetDirty(segment);
    }

    private void EraseCells(LevelSegment segment, List<Vector2Int> worldCells, int segStart)
    {
        Undo.RecordObject(segment, "Erase Cells");
        var soSegment = new SerializedObject(segment);
        var tableProp = soSegment.FindProperty("spawnTable");

        for (int i = tableProp.arraySize - 1; i >= 0; i--)
        {
            var entry = tableProp.GetArrayElementAtIndex(i);
            int row = entry.FindPropertyRelative("rowOffset").intValue;
            int col = entry.FindPropertyRelative("column").intValue;
            int type = entry.FindPropertyRelative("spawnType").intValue;

            foreach (var wc in worldCells)
            {
                int targetLocal = Mathf.Max(0, wc.y - segStart);
                if (col == wc.x && row == targetLocal && (SpawnType)type == brushSpawnType)
                {
                    tableProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
        }

        soSegment.ApplyModifiedProperties();
        EditorUtility.SetDirty(segment);
    }

    private bool HasEntry(SerializedProperty tableProp, int column, int localRow, SpawnType type)
    {
        for (int i = 0; i < tableProp.arraySize; i++)
        {
            var el = tableProp.GetArrayElementAtIndex(i);
            int r = el.FindPropertyRelative("rowOffset").intValue;
            int c = el.FindPropertyRelative("column").intValue;
            int t = el.FindPropertyRelative("spawnType").intValue;
            if (r == localRow && c == column && (SpawnType)t == type) return true;
        }
        return false;
    }

    /// <summary>Finds a runtime SpawnEntry at (localRow, column) inside a segment (fast read).</summary>
    private SpawnEntry FindEntryAt(LevelSegment segment, int localRow, int column)
    {
        if (segment == null || segment.SpawnTable == null) return null;
        for (int i = 0; i < segment.SpawnTable.Count; i++)
        {
            var e = segment.SpawnTable[i];
            if (e != null && e.rowOffset == localRow && e.column == column)
                return e;
        }
        return null;
    }

    private static List<string> ParseTags(string csv)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(csv)) return list;

        var split = csv.Split(',');
        foreach (var raw in split)
        {
            var trimmed = raw?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                list.Add(trimmed);
        }
        return list;
    }
    #endregion

    #region Create First Segment (helper)
    private void CreateFirstSegment()
    {
        if (levelDefinition == null) return;

        var seg = ScriptableObject.CreateInstance<LevelSegment>();
        var soSeg = new SerializedObject(seg);
        soSeg.FindProperty("segmentType").intValue = (int)SegmentType.EnemyWave;
        soSeg.FindProperty("lengthInRows").intValue = 30;
        soSeg.ApplyModifiedPropertiesWithoutUndo();

        var soLevel = new SerializedObject(levelDefinition);
        var segmentsProp = soLevel.FindProperty("segments");
        int idx = segmentsProp.arraySize;
        segmentsProp.InsertArrayElementAtIndex(idx);
        segmentsProp.GetArrayElementAtIndex(idx).objectReferenceValue = seg;
        soLevel.ApplyModifiedPropertiesWithoutUndo();

        manualSegmentIndex = 0;
        EditorUtility.SetDirty(levelDefinition);
        RecomputeSegmentBases();
    }
    #endregion

    #region Validation & Migration (unchanged)
    private void AutoMigrateMisplacedEntries()
    {
        if (levelDefinition == null || levelDefinition.Segments == null) return;
        if (gridConfig == null) { ShowTempStatus("Assign a Grid Config first.", MessageType.Warning); return; }

        RecomputeSegmentBases();

        // Carry definition + color through the migration
        var worldEntries = new List<(SpawnType type, int col, int worldRow, Object payload, List<string> tags, EnemyDefinition def, EnemyHue hue, EnemyTone tone)>();

        // Flatten to world rows
        int baseRow = 0;
        foreach (var seg in levelDefinition.Segments)
        {
            int len = Mathf.Max(0, seg?.LengthInRows ?? 0);
            if (seg != null && seg.SpawnTable != null)
            {
                foreach (var e in seg.SpawnTable)
                {
                    int clampedCol = Mathf.Clamp(e.column, 0, gridConfig.Columns - 1);

                    // Pull definition + (optional) color from entry
                    EnemyDefinition def = e.EnemyDefinition;
                    EnemyHue hue = e.EnemyColor.hue;
                    EnemyTone tone = e.EnemyColor.tone;

                    worldEntries.Add((e.spawnType, clampedCol, baseRow + e.rowOffset, e.payload, e.tags, def, hue, tone));
                }
            }
            baseRow += len;
        }

        // Clear all spawn tables
        foreach (var seg in levelDefinition.Segments)
        {
            if (!seg) continue;
            var so = new SerializedObject(seg);
            var tableProp = so.FindProperty("spawnTable");
            tableProp.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seg);
        }

        // Reinsert into proper segments by world row
        foreach (var (type, col, worldRow, payload, tags, def, hue, tone) in worldEntries)
        {
            if (!TryResolveSegmentAndLocalRow(worldRow, out int segIndex, out int localRow)) continue;

            var seg = levelDefinition.Segments[segIndex];
            var so = new SerializedObject(seg);
            var tableProp = so.FindProperty("spawnTable");

            if (avoidDuplicates && HasEntry(tableProp, col, localRow, type))
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                continue;
            }

            int idx = tableProp.arraySize;
            tableProp.InsertArrayElementAtIndex(idx);
            var el = tableProp.GetArrayElementAtIndex(idx);
            el.FindPropertyRelative("rowOffset").intValue = localRow;
            el.FindPropertyRelative("column").intValue = col;
            el.FindPropertyRelative("spawnType").intValue = (int)type;
            el.FindPropertyRelative("payload").objectReferenceValue = payload;
            el.FindPropertyRelative("count").intValue = 1;
            el.FindPropertyRelative("spacing").intValue = 1;

            // Preserve tags
            var tagsProp = el.FindPropertyRelative("tags");
            tagsProp.arraySize = tags?.Count ?? 0;
            for (int i = 0; i < (tags?.Count ?? 0); i++)
                tagsProp.GetArrayElementAtIndex(i).stringValue = tags[i];

            // NEW: write EnemyDefinition for Enemy or Boss
            if (type == SpawnType.Enemy || type == SpawnType.Boss)
            {
                var defProp = el.FindPropertyRelative("enemyDefinition");
                if (defProp != null) defProp.objectReferenceValue = def;
            }

            // (Optional) restore Enemy color for Enemy
            if (type == SpawnType.Enemy)
            {
                var colorProp = el.FindPropertyRelative("enemyColor");
                if (colorProp != null)
                {
                    colorProp.FindPropertyRelative("hue").enumValueIndex = (int)hue;
                    colorProp.FindPropertyRelative("tone").enumValueIndex = (int)tone;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seg);
        }

        Debug.Log("[GridLevelPainter] Auto-migrate completed.");
    }


    private void ValidateAndFixLevel()
    {
        if (gridConfig == null || levelDefinition == null || levelDefinition.Segments == null)
        {
            ShowTempStatus("Assign Grid Config + Level Definition first.", MessageType.Warning);
            return;
        }

        RecomputeSegmentBases();

        int correctedColumns = 0, reinserted = 0, deleted = 0;

        // Carry definition + color through the validation pass
        var worldEntries = new List<(SpawnType type, int col, int worldRow, Object payload, List<string> tags, EnemyDefinition def, EnemyHue hue, EnemyTone tone)>();

        int baseRow = 0;
        foreach (var seg in levelDefinition.Segments)
        {
            int len = Mathf.Max(0, seg?.LengthInRows ?? 0);
            if (seg != null && seg.SpawnTable != null)
            {
                foreach (var e in seg.SpawnTable)
                {
                    int clampedCol = Mathf.Clamp(e.column, 0, gridConfig.Columns - 1);
                    if (clampedCol != e.column) correctedColumns++;

                    EnemyDefinition def = e.EnemyDefinition;
                    EnemyHue hue = e.EnemyColor.hue;
                    EnemyTone tone = e.EnemyColor.tone;

                    worldEntries.Add((e.spawnType, clampedCol, baseRow + e.rowOffset, e.payload, e.tags, def, hue, tone));
                }
            }
            baseRow += len;
        }

        // Clear all tables
        foreach (var seg in levelDefinition.Segments)
        {
            if (!seg) continue;
            var so = new SerializedObject(seg);
            var tableProp = so.FindProperty("spawnTable");
            tableProp.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seg);
        }

        // Reinsert, discarding out-of-range
        foreach (var (type, col, worldRow, payload, tags, def, hue, tone) in worldEntries)
        {
            if (!TryResolveSegmentAndLocalRow(worldRow, out int segIndex, out int localRow)) { deleted++; continue; }

            var seg = levelDefinition.Segments[segIndex];
            int len = Mathf.Max(0, seg?.LengthInRows ?? 0);
            if (localRow < 0 || localRow >= len) { deleted++; continue; }

            var so = new SerializedObject(seg);
            var tableProp = so.FindProperty("spawnTable");

            if (avoidDuplicates && HasEntry(tableProp, col, localRow, type))
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                continue;
            }

            int idx = tableProp.arraySize;
            tableProp.InsertArrayElementAtIndex(idx);
            var el = tableProp.GetArrayElementAtIndex(idx);
            el.FindPropertyRelative("rowOffset").intValue = localRow;
            el.FindPropertyRelative("column").intValue = col;
            el.FindPropertyRelative("spawnType").intValue = (int)type;
            el.FindPropertyRelative("payload").objectReferenceValue = payload;
            el.FindPropertyRelative("count").intValue = 1;
            el.FindPropertyRelative("spacing").intValue = 1;

            // Preserve tags
            var tagsProp = el.FindPropertyRelative("tags");
            tagsProp.arraySize = tags?.Count ?? 0;
            for (int i = 0; i < (tags?.Count ?? 0); i++)
                tagsProp.GetArrayElementAtIndex(i).stringValue = tags[i];

            // NEW: write EnemyDefinition for Enemy or Boss
            if (type == SpawnType.Enemy || type == SpawnType.Boss)
            {
                var defProp = el.FindPropertyRelative("enemyDefinition");
                if (defProp != null) defProp.objectReferenceValue = def;
            }

            // (Optional) restore Enemy color for Enemy
            if (type == SpawnType.Enemy)
            {
                var colorProp = el.FindPropertyRelative("enemyColor");
                if (colorProp != null)
                {
                    colorProp.FindPropertyRelative("hue").enumValueIndex = (int)hue;
                    colorProp.FindPropertyRelative("tone").enumValueIndex = (int)tone;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seg);
            reinserted++;
        }

        ShowTempStatus($"Validate & Fix complete. Columns clamped: {correctedColumns}, Reinserted: {reinserted}, Deleted: {deleted}.", MessageType.Info);
    }
    #endregion
}
#endif
