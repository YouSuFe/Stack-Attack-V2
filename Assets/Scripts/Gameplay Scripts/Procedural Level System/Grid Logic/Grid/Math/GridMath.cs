using UnityEngine;

public static class GridMath
{
    public static Vector3 GridToWorld(int col, int row, GridConfig grid)
    {
        float x = (col - (grid.Columns - 1) * 0.5f) * grid.CellWidth;
        float y = row * grid.CellHeight;
        return new Vector3(x, y, 0f);
    }

    public static void WorldToGrid(Vector3 world, GridConfig grid, out int col, out int row)
    {
        float cx = world.x / grid.CellWidth + (grid.Columns - 1) * 0.5f;
        float ry = world.y / grid.CellHeight;
        col = Mathf.RoundToInt(cx);
        row = Mathf.RoundToInt(ry);
    }
}