using UnityEngine;

public enum GridTopology
{
    Rectangle,
    Hex,
    Octagon
}

public enum HexOrientation
{
    PointyTop, // vertical step ≈ 1.5 * size
    FlatTop    // horizontal step ≈ 1.5 * size
}

public enum HexOffsetType
{
    OddR,  // use with PointyTop (odd rows offset)
    EvenR, // use with PointyTop (even rows offset)
    OddQ,  // use with FlatTop (odd columns offset)
    EvenQ  // use with FlatTop (even columns offset)
}

public enum OctLayout
{
    Diamond,        // rows staggered; octagons touch along diagonal sides (default)
    //Square          // regular square lattice; interstitial little squares (4.8.8 tiling)
}

[CreateAssetMenu(menuName = "Game/Level/Grid Config")]
public class GridConfig : ScriptableObject
{
    #region Grid Dimensions
    [Header("Origin")]
    [Tooltip("World Y offset for grid row 0 (applies to all topologies, painter & gizmos).")]
    [SerializeField] private float originY = 5f;

    [Header("Topology")]
    [SerializeField] private GridTopology topology = GridTopology.Rectangle;

    [Header("Shared Grid Settings")]
    [Tooltip("Number of columns (horizontal cells) across the grid.\n" +
             "Defines how many lanes or tiles fit side-by-side.")]
    [SerializeField, Range(1, 20)] private int columns = 5;

    [Header("Rectangle Grid Settings")]
    [Tooltip("Width of each grid cell in world units.\n" +
             "Determines horizontal spacing between cell centers.")]
    [SerializeField, Range(0.1f, 5f)] private float cellWidth = 1f;

    [Tooltip("Height of each grid cell in world units.\n" +
             "Determines vertical spacing between cell centers.")]
    [SerializeField, Range(0.1f, 5f)] private float cellHeight = 1f;

    [Header("Hex Grid Settings")]
    [Tooltip("PointyTop: flats left/right; FlatTop: flats up/down.")]
    [SerializeField] private HexOrientation hexOrientation = HexOrientation.PointyTop;

    [Tooltip("Radius of the hex (center to any corner).")]
    [SerializeField, Range(0.1f, 5f)] private float  hexSize = 0.5f;

    [Tooltip("Offset addressing scheme. Use R-variants for PointyTop, Q-variants for FlatTop.")]
    [SerializeField] private HexOffsetType hexOffset = HexOffsetType.EvenR;

    [Header("Octagon Grid Settings")]
    [Tooltip("Apothem (center to a flat side) of the octagon in world units.")]
    [SerializeField, Range(0.05f, 5f)] private float octApothem = 0.5f;

    [Tooltip("Layout pattern of octagon centers. Diamond = staggered rows (touch via diagonal sides). Square = square lattice (interstitial squares).")]
    [SerializeField] private OctLayout octLayout = OctLayout.Diamond; // default

    #endregion

    #region Editor Visualization
    [Header("Editor Visualization (used only in Scene view)")]
    
    [Tooltip("How many rows of grid lines are visible in the Scene view Gizmos.\n" +
             "Purely visual — does not affect gameplay or spawning.")]
    [SerializeField, Range(1, 100)] private int previewRows = 15;

    [Tooltip("Extra rows to draw ABOVE the visible grid in the Scene view.\n" +
             "Useful for previewing or painting slightly beyond the camera view (scrolling levels).")]
    [SerializeField, Range(0, 100)] private int extraTopRows = 0;

    [Tooltip("Extra rows to draw BELOW the visible grid in the Scene view.\n" +
             "Allows you to see or paint a few rows behind the starting area (for debugging or spawn padding).")]
    [SerializeField, Range(0, 100)] private int extraBottomRows = 0;
    #endregion

    #region Public Accessors
    public float OriginY => originY;

    public GridTopology Topology => topology;
    public int Columns => columns;
    public float CellWidth => cellWidth;
    public float CellHeight => cellHeight;

    public HexOrientation HexOrientation => hexOrientation;
    public float HexSize => hexSize;
    public HexOffsetType HexOffset => hexOffset;

    public float OctApothem => octApothem;
    public OctLayout OctLayout => octLayout;

    public int PreviewRows => previewRows;
    public int ExtraTopRows => extraTopRows;
    public int ExtraBottomRows => extraBottomRows;
    #endregion
}
