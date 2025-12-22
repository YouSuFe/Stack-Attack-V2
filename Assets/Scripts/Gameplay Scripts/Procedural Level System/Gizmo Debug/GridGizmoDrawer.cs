// Assets/Editor/GridGizmoDrawer.cs
// Stripe-aware gizmo drawer that visualizes mixed grid topologies inside each segment.
// - Uses SegmentStripeMap.Build(...) + GridStripeAdapter for world<->grid
// - Draws backdrop lattice per stripe (Rectangle / Hex / Octagon)
// - Draws placed entries (SpawnTable) with correct polygon per stripe
// - Draws per-segment colored bands with EXACT height by summing stripe heights (rows*step + yGap)
//
// Requires: GridConfig, LevelDefinition, LevelSegment, SpawnEntry/SpawnType,
//           SegmentStripeMap, GridStripeAdapter, HexGridMath, OctGridMath
//
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class GridGizmoDrawer : MonoBehaviour
{
    #region Serialized Targets
    [Header("Targets")]
    [Tooltip("Default grid config when a segment has no stripes (used also for fallback width calc).")]
    [SerializeField] private GridConfig gridConfig;

    [Tooltip("LevelDefinition to visualize")]
    [SerializeField] private LevelDefinition levelDefinition;

    [Tooltip("Used to resolve Enemy hue/tone selections into actual Colors for drawing.")]
    [SerializeField] private EnemyColorPalette enemyPalette;  
    #endregion

    #region Visual Toggles
    [Header("What to Draw")]
    [SerializeField] private bool drawSegmentBands = true;
    [SerializeField] private bool labelSegments = true;
    [SerializeField] private bool drawGridBackdrop = true;
    [SerializeField] private bool drawPlacedEntries = true;
    #endregion

    #region Style
    [Header("Style")]
    [Tooltip("Alpha for the lattice lines (backdrop)")]
    [Range(0f, 1f)] [SerializeField] private float latticeAlpha = 0.15f;

    [Tooltip("Fill alpha for placed entries")]
    [Range(0f, 1f)] [SerializeField] private float placedFillAlpha = 0.25f;

    [Tooltip("Border color for placed entries")]
    [SerializeField] private Color placedBorderColor = new Color(1, 1, 1, 0.9f);

    [Tooltip("Border thickness (Handles AA polyline thickness)")]
    [SerializeField] private float placedBorderThickness = 2.0f;

    [Header("Segment Bands")]
    [Tooltip("Segment colors (auto-fills if shorter than segment count).")]
    [SerializeField]
    private List<Color> segmentColors = new List<Color>()
    {
        new Color(0.25f, 0.55f, 0.95f, 0.08f),
        new Color(0.95f, 0.55f, 0.25f, 0.08f),
        new Color(0.45f, 0.95f, 0.55f, 0.08f),
        new Color(0.85f, 0.45f, 0.95f, 0.08f),
        new Color(0.95f, 0.85f, 0.25f, 0.08f),
    };

    [Tooltip("Extra alpha added to segment color for band fill.")]
    [Range(0f, 0.25f)] [SerializeField] private float segmentBandExtraAlpha = 0.05f;

    [Tooltip("Label color for segment titles.")]
    [SerializeField] private Color segmentLabelColor = new Color(1f, 1f, 1f, 0.85f);
    #endregion

    #region Layout Cache
    // Per-segment world-row bases (like in painter)
    private readonly List<int> segmentBaseWorldRows = new List<int>();
    private int totalWorldRows;

    // Stripe cache per segment
    private readonly List<List<StripeInfo>> segmentStripeMaps = new List<List<StripeInfo>>();
    #endregion

    #region Gizmo Entry
    private void OnDrawGizmos()
    {
        if (gridConfig == null || levelDefinition == null || levelDefinition.Segments == null || levelDefinition.Segments.Count == 0)
            return;

        RecomputeSegmentBases();
        RebuildStripeMaps();

        if (drawSegmentBands) DrawSegmentBands_Exact();

        if (drawGridBackdrop) DrawGridBackdrop_Stripes();

        if (drawPlacedEntries) DrawPlacedEntries_Stripes();
    }
    #endregion

    #region Segment Bases
    private void RecomputeSegmentBases()
    {
        segmentBaseWorldRows.Clear();
        totalWorldRows = 0;
        foreach (var seg in levelDefinition.Segments)
        {
            segmentBaseWorldRows.Add(totalWorldRows);
            totalWorldRows += seg ? Mathf.Max(0, seg.LengthInRows) : 0;
        }
    }
    #endregion

    #region Stripe Map Build
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
                gridConfig,  // default per-level when no stripes on segment
                RowStepY     // callback to compute Y step per row for given config
            );

            segmentStripeMaps.Add(stripes);
        }
    }
    #endregion

    #region Row Step Helper
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
    #endregion

    #region Segment Bands (EXACT)
    // Draws per-segment colored bands with exact vertical size by summing all stripes:
    //   exactHeight = Σ (stripeRows * RowStepY(stripeConfig) + yGap)
    private void DrawSegmentBands_Exact()
    {
        if (levelDefinition?.Segments == null) return;

#if UNITY_EDITOR
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
#endif

        EnsureSegmentColors(levelDefinition.Segments.Count);

        for (int segIndex = 0; segIndex < levelDefinition.Segments.Count; segIndex++)
        {
            var seg = levelDefinition.Segments[segIndex];
            if (!seg) continue;

            // Fallback if there are no stripes (treat as single stripe using default grid)
            float segHeightY = 0f;
            float segYStart = 0f;

            if (segIndex < segmentStripeMaps.Count && segmentStripeMaps[segIndex] != null && segmentStripeMaps[segIndex].Count > 0)
            {
                var stripes = segmentStripeMaps[segIndex];

                // yStart = yBase of FIRST stripe
                segYStart = stripes[0].yBase;

                // exact height = (lastStripeEndY - firstStripeStartY)
                var last = stripes[stripes.Count - 1];
                float lastStripeHeight = (last.segmentLocalEndRow - last.segmentLocalStartRow) * RowStepY(last.config);
                segHeightY = (last.yBase + lastStripeHeight) - segYStart;
            }
            else
            {
                // No stripes: approximate with default grid rows*step
                int baseWorld = segmentBaseWorldRows[segIndex];
                segYStart = gridConfig.OriginY + baseWorld * RowStepY(gridConfig);
                segHeightY = Mathf.Max(0, seg.LengthInRows) * RowStepY(gridConfig);
            }

            // Compute a reasonable width for the band
            float totalWidth = ComputeApproxWidthForSegment(segIndex);

            // Color (segment-based)
            Color band = segmentColors[segIndex % segmentColors.Count];
            band.a = Mathf.Clamp01(band.a + segmentBandExtraAlpha);

#if UNITY_EDITOR
            Handles.color = band;
            Vector3 p0 = new Vector3(-totalWidth * 0.5f, segYStart, 0f);
            Vector3 p1 = new Vector3(+totalWidth * 0.5f, segYStart, 0f);
            Vector3 p2 = new Vector3(+totalWidth * 0.5f, segYStart + segHeightY, 0f);
            Vector3 p3 = new Vector3(-totalWidth * 0.5f, segYStart + segHeightY, 0f);
            Handles.DrawAAConvexPolygon(p0, p1, p2, p3);

            if (labelSegments)
            {
                Handles.BeginGUI();
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                {
                    Vector3 worldLabel = new Vector3(-totalWidth * 0.5f + 0.25f, segYStart + 0.15f, 0f);
                    Vector2 gui = HandleUtility.WorldToGUIPoint(worldLabel);
                    var rect = new Rect(gui.x, gui.y, 320, 22);
                    GUI.color = segmentLabelColor;
                    GUI.Label(rect, $"Segment[{segIndex}] {(seg ? seg.SegmentType.ToString() : "Null")} (rows={seg?.LengthInRows})");
                    GUI.color = Color.white;
                }
                Handles.EndGUI();
            }
#else
            Gizmos.color = band;
            Gizmos.DrawCube(new Vector3(0f, segYStart + segHeightY * 0.5f, 0f), new Vector3(totalWidth, Mathf.Max(0.01f, segHeightY), 0.001f));
#endif
        }
    }

    private void EnsureSegmentColors(int count)
    {
        if (segmentColors == null) segmentColors = new List<Color>();
        while (segmentColors.Count < count)
        {
            int i = segmentColors.Count;
            // deterministic HSV palette
            float h = Mathf.Repeat((i * 0.1618f), 1f);
            var c = Color.HSVToRGB(h, 0.6f, 1f);
            c.a = 0.08f;
            segmentColors.Add(c);
        }
    }

    private float ComputeApproxWidthForSegment(int segIndex)
    {
        // If stripes exist, use the widest stripe width; else fall back to default config width.
        float width = 0f;

        if (segIndex < segmentStripeMaps.Count && segmentStripeMaps[segIndex] != null && segmentStripeMaps[segIndex].Count > 0)
        {
            foreach (var s in segmentStripeMaps[segIndex])
                width = Mathf.Max(width, ApproxWidth(s.config));
        }
        else
        {
            width = ApproxWidth(gridConfig);
        }

        return width;
    }

    private float ApproxWidth(GridConfig cfg)
    {
        switch (cfg.Topology)
        {
            case GridTopology.Rectangle:
                return cfg.Columns * cfg.CellWidth;
            case GridTopology.Hex:
                // width spanned by N columns for the chosen orientation
                float colStep = (cfg.HexOrientation == HexOrientation.PointyTop)
                    ? Mathf.Sqrt(3f) * cfg.HexSize
                    : 1.5f * cfg.HexSize;
                return (cfg.Columns - 1) * colStep + (2f * cfg.HexSize);
            case GridTopology.Octagon:
                // approximate to polygon diameter
                float R = cfg.OctApothem / Mathf.Cos(Mathf.PI / 8f);
                return cfg.Columns * (2f * R * 0.95f);
            default:
                return cfg.Columns;
        }
    }
    #endregion

    #region Backdrop (stripe-aware)
    private void DrawGridBackdrop_Stripes()
    {
#if UNITY_EDITOR
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = new Color(1f, 1f, 1f, latticeAlpha);
#endif
        if (segmentStripeMaps.Count == 0) return;

        for (int segIndex = 0; segIndex < segmentStripeMaps.Count; segIndex++)
        {
            var stripes = segmentStripeMaps[segIndex];
            if (stripes == null) continue;

            foreach (var s in stripes)
            {
                int rowsInStripe = Mathf.Max(0, s.segmentLocalEndRow - s.segmentLocalStartRow);

                if (s.topology == GridTopology.Rectangle)
                {
                    for (int r = 0; r < rowsInStripe; r++)
                    {
                        for (int c = 0; c < s.config.Columns; c++)
                        {
                            Vector3 center = GridStripeAdapter.GridToWorld(c, r, s);
                            float w = s.config.CellWidth;
                            float h = s.config.CellHeight;

                            Vector3 p0 = center + new Vector3(-w * 0.5f, -h * 0.5f, 0f);
                            Vector3 p1 = center + new Vector3(+w * 0.5f, -h * 0.5f, 0f);
                            Vector3 p2 = center + new Vector3(+w * 0.5f, +h * 0.5f, 0f);
                            Vector3 p3 = center + new Vector3(-w * 0.5f, +h * 0.5f, 0f);
#if UNITY_EDITOR
                            Handles.DrawAAPolyLine(1.25f, p0, p1, p2, p3, p0);
#else
                            Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2);
                            Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);
#endif
                        }
                    }
                }
                else if (s.topology == GridTopology.Hex)
                {
                    var verts = new Vector3[6];
                    for (int r = 0; r < rowsInStripe; r++)
                    {
                        for (int c = 0; c < s.config.Columns; c++)
                        {
                            Vector3 center = GridStripeAdapter.GridToWorld(c, r, s);
                            HexGridMath.GetHexCorners(new Vector2(center.x, center.y), s.config.HexSize, s.config.HexOrientation, verts);
#if UNITY_EDITOR
                            Handles.DrawAAPolyLine(1.25f, verts[0], verts[1], verts[2], verts[3], verts[4], verts[5], verts[0]);
#else
                            for (int i = 0; i < 6; i++) Gizmos.DrawLine(verts[i], verts[(i + 1) % 6]);
#endif
                        }
                    }
                }
                else // Octagon
                {
                    var verts = new Vector3[8];
                    for (int r = 0; r < rowsInStripe; r++)
                    {
                        for (int c = 0; c < s.config.Columns; c++)
                        {
                            Vector3 center = GridStripeAdapter.GridToWorld(c, r, s);
                            OctGridMath.GetOctCorners(new Vector2(center.x, center.y), s.config.OctApothem, verts);
#if UNITY_EDITOR
                            Handles.DrawAAPolyLine(1.25f,
                                verts[0], verts[1], verts[2], verts[3], verts[4], verts[5], verts[6], verts[7], verts[0]);
#else
                            for (int i = 0; i < 8; i++) Gizmos.DrawLine(verts[i], verts[(i + 1) % 8]);
#endif
                        }
                    }
                }
            }
        }
    }
    #endregion

    #region Placed Entries (stripe-aware)
    private void DrawPlacedEntries_Stripes()
    {
        if (levelDefinition?.Segments == null) return;

#if UNITY_EDITOR
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
#endif

        for (int segIndex = 0; segIndex < levelDefinition.Segments.Count; segIndex++)
        {
            var seg = levelDefinition.Segments[segIndex];
            if (!seg) continue;

            // stripes for this segment
            if (segIndex >= segmentStripeMaps.Count) continue;
            var stripes = segmentStripeMaps[segIndex];
            if (stripes == null || stripes.Count == 0) continue;

            var table = seg.SpawnTable;
            if (table == null) continue;

            foreach (var e in table)
            {
                if (e == null) continue;

                // compute world row for this entry
                int worldRow = segmentBaseWorldRows[segIndex] + Mathf.Max(0, e.rowOffset);

                // find stripe that contains this worldRow
                StripeInfo stripe = default;
                bool foundStripe = false;
                for (int s = 0; s < stripes.Count; s++)
                {
                    var st = stripes[s];
                    if (worldRow >= st.worldStartRow && worldRow < st.worldEndRow)
                    {
                        stripe = st; foundStripe = true; break;
                    }
                }
                if (!foundStripe) continue;

                // stripe-local row and clamped col
                int rowInStripe = worldRow - stripe.worldStartRow;
                int col = Mathf.Clamp(e.column, 0, stripe.config.Columns - 1);

                // cell center
                Vector3 center = GridStripeAdapter.GridToWorld(col, rowInStripe, stripe);

                // colors
                Color fill = ResolveEntryColor(e);
                fill.a = placedFillAlpha;

                if (stripe.topology == GridTopology.Rectangle)
                {
                    float w = stripe.config.CellWidth;
                    float h = stripe.config.CellHeight;

#if UNITY_EDITOR
                    Handles.color = fill;
                    Handles.DrawAAConvexPolygon(
                        center + new Vector3(-w * 0.5f, -h * 0.5f, 0f),
                        center + new Vector3(+w * 0.5f, -h * 0.5f, 0f),
                        center + new Vector3(+w * 0.5f, +h * 0.5f, 0f),
                        center + new Vector3(-w * 0.5f, +h * 0.5f, 0f)
                    );

                    if (placedBorderThickness > 0f)
                    {
                        Handles.color = placedBorderColor;
                        Handles.DrawAAPolyLine(placedBorderThickness,
                            center + new Vector3(-w * 0.5f, -h * 0.5f, 0f),
                            center + new Vector3(+w * 0.5f, -h * 0.5f, 0f),
                            center + new Vector3(+w * 0.5f, +h * 0.5f, 0f),
                            center + new Vector3(-w * 0.5f, +h * 0.5f, 0f),
                            center + new Vector3(-w * 0.5f, -h * 0.5f, 0f)
                        );
                    }
#else
                    Gizmos.color = fill;
                    Gizmos.DrawCube(center, new Vector3(w * 0.92f, h * 0.92f, 0.001f));
                    Gizmos.color = placedBorderColor;
                    Vector3 p0 = center + new Vector3(-w * 0.5f, -h * 0.5f, 0f);
                    Vector3 p1 = center + new Vector3(+w * 0.5f, -h * 0.5f, 0f);
                    Vector3 p2 = center + new Vector3(+w * 0.5f, +h * 0.5f, 0f);
                    Vector3 p3 = center + new Vector3(-w * 0.5f, +h * 0.5f, 0f);
                    Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2); Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);
#endif
                }
                else if (stripe.topology == GridTopology.Hex)
                {
                    var verts = new Vector3[6];
                    HexGridMath.GetHexCorners(new Vector2(center.x, center.y),
                                              stripe.config.HexSize,
                                              stripe.config.HexOrientation,
                                              verts);
#if UNITY_EDITOR
                    Handles.color = fill;
                    Handles.DrawAAConvexPolygon(verts);
                    if (placedBorderThickness > 0f)
                    {
                        Handles.color = placedBorderColor;
                        Handles.DrawAAPolyLine(placedBorderThickness,
                            verts[0], verts[1], verts[2], verts[3], verts[4], verts[5], verts[0]);
                    }
#else
                    Gizmos.color = placedBorderColor;
                    for (int i = 0; i < 6; i++) Gizmos.DrawLine(verts[i], verts[(i + 1) % 6]);
#endif
                }
                else // Octagon
                {
                    var verts = new Vector3[8];
                    OctGridMath.GetOctCorners(new Vector2(center.x, center.y),
                                              stripe.config.OctApothem,
                                              verts);
#if UNITY_EDITOR
                    Handles.color = fill;
                    Handles.DrawAAConvexPolygon(verts);
                    if (placedBorderThickness > 0f)
                    {
                        Handles.color = placedBorderColor;
                        Handles.DrawAAPolyLine(placedBorderThickness,
                            verts[0], verts[1], verts[2], verts[3], verts[4], verts[5], verts[6], verts[7], verts[0]);
                    }
#else
                    Gizmos.color = placedBorderColor;
                    for (int i = 0; i < 8; i++) Gizmos.DrawLine(verts[i], verts[(i + 1) % 8]);
#endif
                }
            }
        }
    }
    #endregion

    #region Color helpers

    /// <summary>
    /// Base color for non-Enemy types (visual legend).
    /// Add entries for all your new SpawnType values so each is distinct.
    /// </summary>
    private Color GetTypeBaseColor(SpawnType type)
    {
        switch (type)
        {
            case SpawnType.Coin: return new Color(1.00f, 0.85f, 0.20f, 1f);
            case SpawnType.Trap: return new Color(0.95f, 0.55f, 0.15f, 1f);
            case SpawnType.Boss: return new Color(0.90f, 0.40f, 0.60f, 1f);
            case SpawnType.Decoration: return new Color(0.65f, 0.65f, 0.75f, 1f);
            case SpawnType.SegmentEndMarker: return new Color(0.30f, 0.90f, 0.90f, 1f);
            case SpawnType.Multiplier: return new Color(0.60f, 0.95f, 0.60f, 1f);
            case SpawnType.Rage: return new Color(0.95f, 0.30f, 0.30f, 1f);
            case SpawnType.SkillFiller: return new Color(0.40f, 0.80f, 1.00f, 1f);
            case SpawnType.Bomb: return new Color(0.25f, 0.25f, 0.25f, 1f);
            case SpawnType.Wall: return new Color(0.55f, 0.55f, 0.60f, 1f);
            // Enemy handled in ResolveEntryColor (via palette)
            default: return new Color(1f, 1f, 1f, 1f);
        }
    }

    /// <summary>
    /// Final color for an entry. If Enemy, resolve by palette using the entry's hue/tone;
    /// otherwise fall back to base type color.
    /// </summary>
    private Color ResolveEntryColor(SpawnEntry entry)
    {
        if (entry == null) return Color.white;

        if (entry.spawnType == SpawnType.Enemy)
        {
            if (enemyPalette != null)
            {
                // Your SpawnEntry now holds the selection. Prefer a direct getter if you added one.
                // If you kept EnemyColor as a private field with a public property (EnemyColor),
                // use entry.EnemyColor.hue and entry.EnemyColor.tone here.
#if true
                // Using public property route:
                var selHue = entry.EnemyColor.hue;
                var selTone = entry.EnemyColor.tone;
                if (enemyPalette.TryResolve(selHue, selTone, out var resolved))
                    return resolved;
#else
            // (Alternative: if you didn't expose EnemyColor publicly, you could serialize-reflect, but not needed here.)
#endif
            }
            // Fallback if no palette or hue missing
            return new Color(0.95f, 0.25f, 0.25f, 1f);
        }

        return GetTypeBaseColor(entry.spawnType);
    }
    #endregion
}
