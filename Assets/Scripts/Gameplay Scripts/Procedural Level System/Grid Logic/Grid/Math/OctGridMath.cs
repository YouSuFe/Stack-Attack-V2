using UnityEngine;

public static class OctGridMath
{
    // Geometry: apothem = distance from center to a flat side.
    // Circumradius R = a / cos(22.5°).
    private const float DEG22_5 = 22.5f;
    private static readonly float COS22_5 = Mathf.Cos(Mathf.Deg2Rad * DEG22_5);
    //private static readonly float SQRT2 = 1.41421356237f;

    #region Public API (centers)
    public static Vector3 GridToWorld(int col, int row, GridConfig grid)
    {
        // Diamond layout only: side-touch on N/E/S/W
        float a = grid.OctApothem;
        float step = 2f * a;

        // Center horizontally to match your other grids; no vertical centering
        float x = (col - (grid.Columns - 1) * 0.5f) * step;
        float y = row * step;

        return new Vector3(x, y, 0f);
    }

    public static void WorldToGrid(Vector3 world, GridConfig grid, out int col, out int row)
    {
        // Inverse of the above (diamond only)
        float a = grid.OctApothem;
        float step = 2f * a;

        float cx = world.x / step + (grid.Columns - 1) * 0.5f;
        float ry = world.y / step;

        col = Mathf.RoundToInt(cx);
        row = Mathf.RoundToInt(ry);

        // Safety: confirm with polygon test & snap to nearest neighbor if needed
        SnapToNearestIfOutside(world, grid, ref col, ref row);
    }

    #endregion

    #region Octagon polygon (for gizmos/hover tests)
    public static void GetOctCorners(Vector2 center, float apothem, Vector3[] outVerts)
    {
        // Axis-aligned regular octagon (flats on +X/-X/+Y/-Y)
        // Vertices every 45°, starting from 22.5° to make the first edge horizontal.
        float R = apothem / COS22_5;
        for (int i = 0; i < 8; i++)
        {
            float ang = (22.5f + i * 45f) * Mathf.Deg2Rad;
            float vx = center.x + R * Mathf.Cos(ang);
            float vy = center.y + R * Mathf.Sin(ang);
            outVerts[i] = new Vector3(vx, vy, 0f);
        }
    }

    public static bool PointInOct(Vector2 p, Vector3[] octVerts)
    {
        // Standard winding PIP
        bool inside = false;
        for (int i = 0, j = 7; i < 8; j = i++)
        {
            Vector3 vi = octVerts[i];
            Vector3 vj = octVerts[j];
            bool intersect = ((vi.y > p.y) != (vj.y > p.y)) &&
                             (p.x < (vj.x - vi.x) * (p.y - vi.y) / (vj.y - vi.y + Mathf.Epsilon) + vi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }
    #endregion

    #region Helpers
    private static void SnapToNearestIfOutside(Vector3 world, GridConfig grid, ref int col, ref int row)
    {
        // Verify current guess; if outside, test its 8 neighbors and keep the best hit.
        var verts = new Vector3[8];
        Vector3 center = GridToWorld(col, row, grid);
        GetOctCorners(center, grid.OctApothem, verts);
        if (PointInOct(world, verts)) return;

        int bestC = col, bestR = row;
        float bestSqr = float.PositiveInfinity;
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int nc = col + dc, nr = row + dr;
                Vector3 c2 = GridToWorld(nc, nr, grid);
                GetOctCorners(c2, grid.OctApothem, verts);
                if (PointInOct(world, verts))
                {
                    float d2 = (world - c2).sqrMagnitude;
                    if (d2 < bestSqr) { bestSqr = d2; bestC = nc; bestR = nr; }
                }
            }
        col = bestC; row = bestR;
    }
    #endregion
}

