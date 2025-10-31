using System;
using System.Collections.Generic;
using UnityEngine;

public enum SpaceAlignMode
{
    UseGlobal = 0,   // Use the sequencer's defaultSpaceAlignMode (set in LevelSegmentSequencer)
    None = 1,        // Ignore the spawn anchor entirely for this Space segment (no vertical shift)
    FirstRowToAnchor = 2, // Align Space segment's FIRST row to the anchor Y (same behavior as non-space)
    LastRowToAnchor = 3   // Align Space segment's LAST row to the anchor Y (recommended: shows the “final Y” exactly at anchor)
}

[CreateAssetMenu(menuName = "Level/Segment")]
public class LevelSegment : ScriptableObject
{
    #region Serialized Fields
    [Header("Space Segment Mode")]
    [Tooltip(
        "Only used when SegmentType is Space/Spacer.\n" +
        "Choose how THIS Space segment should align vertically relative to the spawn anchor:\n" +
        "- UseGlobal: defer to the Sequencer's defaultSpaceAlignMode.\n" +
        "- None: ignore anchor (no vertical shift).\n" +
        "- FirstRowToAnchor: place the first row on the anchor Y.\n" +
        "- LastRowToAnchor: place the last row (e.g., end marker) on the anchor Y."
    )]
    [SerializeField] private SpaceAlignMode spaceAlignOverride = SpaceAlignMode.LastRowToAnchor;

    [Header("General Segment Settings")]
    [SerializeField] private SegmentType segmentType = SegmentType.EnemyWave;
    [SerializeField, Range(0, 2000)] private int lengthInRows = 30;
    [SerializeField] private List<SpawnEntry> spawnTable = new();

    [Serializable]
    public class GridStripe
    {
        [Tooltip("Which grid topology to use in this vertical stripe.")]
        public GridTopology topology = GridTopology.Rectangle;

        [Tooltip("GridConfig to use for this stripe (size, columns, hex/orientation/offset, etc.).")]
        public GridConfig config;

        [Tooltip("How many segment-local rows in this stripe.")]
        public int rows = 0;

        [Tooltip("Optional vertical gap BEFORE this stripe (world Y units).")]
        public float gapBeforeY = 2f;
    }

    [SerializeField, Tooltip("Optional mixed-grid layout inside this segment. If empty, the whole segment uses the level's default grid.")]
    private List<GridStripe> stripes = new List<GridStripe>();
    #endregion

    #region Public Properties
    /// <summary>
    /// Per-Space-segment alignment override.
    /// If this is UseGlobal (default), the Sequencer's 'defaultSpaceAlignMode' decides.
    /// Otherwise, the chosen value here (None/FirstRowToAnchor/LastRowToAnchor) is used just for THIS asset.
    /// </summary>
    public SpaceAlignMode SpaceAlignOverride => spaceAlignOverride;

    public SegmentType SegmentType => segmentType;
    public int LengthInRows => lengthInRows;
    public List<SpawnEntry> SpawnTable => spawnTable;

    /// <summary>Read-only view of stripes. If empty, segment uses the level's default grid.</summary>
    public IReadOnlyList<GridStripe> Stripes => stripes;
    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (stripes != null && stripes.Count > 0)
        {
            int sum = 0;
            foreach (var s in stripes)
                sum += Mathf.Max(0, s?.rows ?? 0);

            if (sum != lengthInRows)
            {
                Debug.LogWarning($"[LevelSegment] '{name}': Stripe rows ({sum}) do not equal LengthInRows ({lengthInRows}). The last stripe will be clamped at runtime.");
            }
        }
    }
#endif
}
