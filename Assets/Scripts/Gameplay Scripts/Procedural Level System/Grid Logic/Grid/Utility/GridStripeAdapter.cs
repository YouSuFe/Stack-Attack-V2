using UnityEngine;

public static class GridStripeAdapter
{
    // Compute horizontal half-width of a hex mosaic (for centering), per your config.
    private static float HexHalfWidth(GridConfig cfg)
    {
        // Horizontal step between columns:
        //  - PointyTop: width per column = sqrt(3) * size
        //  - FlatTop  : width per column = 1.5 * size
        float stepX = (cfg.HexOrientation == HexOrientation.PointyTop)
            ? Mathf.Sqrt(3f) * cfg.HexSize
            : 1.5f * cfg.HexSize;

        float width = (cfg.Columns - 1) * stepX;
        return width * 0.5f;
    }

    public static Vector3 GridToWorld(int col, int stripeLocalRow, in StripeInfo stripe)
    {
        Vector3 center;

        switch (stripe.topology)
        {
            case GridTopology.Rectangle:
                center = GridMath.GridToWorld(col, stripeLocalRow, stripe.config);
                break;

            case GridTopology.Hex:
                {
                    // Offset -> Axial -> World (your API)
                    var a = HexGridMath.OffsetToAxial(col, stripeLocalRow,
                                                      stripe.config.HexOrientation,
                                                      stripe.config.HexOffset);

                    Vector2 pos = HexGridMath.AxialToWorld(a, stripe.config.HexSize, stripe.config.HexOrientation);
                    center = new Vector3(pos.x, pos.y, 0f);

                    // horizontal centering like your gizmos/painter
                    center.x -= HexHalfWidth(stripe.config);
                    break;
                }

            case GridTopology.Octagon:
                center = OctGridMath.GridToWorld(col, stripeLocalRow, stripe.config);
                break;

            default:
                center = Vector3.zero;
                break;
        }

        // Add the stripe's Y base offset
        center.y += stripe.yBase;
        return center;
    }

    public static bool WorldToGrid(Vector3 world, in StripeInfo stripe, out int col, out int stripeLocalRow)
    {
        // Make it stripe-local in Y
        world.y -= stripe.yBase;

        switch (stripe.topology)
        {
            case GridTopology.Rectangle:
                GridMath.WorldToGrid(world, stripe.config, out col, out stripeLocalRow);
                return true;

            case GridTopology.Hex:
                {
                    // inverse of centering
                    world.x += HexHalfWidth(stripe.config);

                    // World -> Axial -> Offset (your API)
                    var axial = HexGridMath.WorldToAxial(new Vector2(world.x, world.y),
                                                         stripe.config.HexSize,
                                                         stripe.config.HexOrientation);

                    HexGridMath.AxialToOffset(axial,
                                              stripe.config.HexOrientation,
                                              stripe.config.HexOffset,
                                              out col, out stripeLocalRow);
                    return true;
                }

            case GridTopology.Octagon:
                OctGridMath.WorldToGrid(world, stripe.config, out col, out stripeLocalRow);
                return true;
        }

        col = stripeLocalRow = 0;
        return false;
    }
}
