using System.Collections.Generic;
using UnityEngine;

public static class HexGridMath
{
    #region Types
    // Axial (q, r). Cube is (x=q, y=-q-r, z=r).
    public struct Axial
    {
        public int q, r;
        public Axial(int q, int r) { this.q = q; this.r = r; }
        public static Axial operator +(Axial a, Axial b) => new Axial(a.q + b.q, a.r + b.r);
        public static Axial operator -(Axial a, Axial b) => new Axial(a.q - b.q, a.r - b.r);
    }

    struct CubeF { public float x, y, z; public CubeF(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }

    public static readonly Axial[] DIRS = new[]
    {
        new Axial(+1, 0), new Axial(+1, -1), new Axial(0, -1),
        new Axial(-1, 0), new Axial(-1, +1), new Axial(0, +1)
    };
    #endregion

    #region Offset <-> Axial  (addressing kept as (col,row) like rectangles)
    public static Axial OffsetToAxial(int col, int row, HexOrientation o, HexOffsetType off)
    {
        if (o == HexOrientation.PointyTop)
        {
            bool even = (row & 1) == 0;
            int q = col - (((off == HexOffsetType.EvenR) && even) || ((off == HexOffsetType.OddR) && !even) ? row >> 1 : (row + 1) >> 1);
            return new Axial(q, row);
        }
        else
        {
            bool even = (col & 1) == 0;
            int r = row - (((off == HexOffsetType.EvenQ) && even) || ((off == HexOffsetType.OddQ) && !even) ? col >> 1 : (col + 1) >> 1);
            return new Axial(col, r);
        }
    }

    public static void AxialToOffset(Axial a, HexOrientation o, HexOffsetType off, out int col, out int row)
    {
        if (o == HexOrientation.PointyTop)
        {
            bool even = (a.r & 1) == 0;
            col = a.q + (((off == HexOffsetType.EvenR) && even) || ((off == HexOffsetType.OddR) && !even) ? a.r >> 1 : (a.r + 1) >> 1);
            row = a.r;
        }
        else
        {
            bool even = (a.q & 1) == 0;
            col = a.q;
            row = a.r + (((off == HexOffsetType.EvenQ) && even) || ((off == HexOffsetType.OddQ) && !even) ? a.q >> 1 : (a.q + 1) >> 1);
        }
    }
    #endregion

    #region World <-> Axial
    public static Vector2 AxialToWorld(Axial a, float size, HexOrientation o)
    {
        if (o == HexOrientation.PointyTop)
        {
            float w = Mathf.Sqrt(3f) * size;
            float vx = w * (a.q + a.r * 0.5f);
            float vy = 1.5f * size * a.r;
            return new Vector2(vx, vy);
        }
        else
        {
            float h = Mathf.Sqrt(3f) * size;
            float vx = 1.5f * size * a.q;
            float vy = h * (a.r + a.q * 0.5f);
            return new Vector2(vx, vy);
        }
    }

    public static Axial WorldToAxial(Vector2 p, float size, HexOrientation o)
    {
        CubeF c;
        if (o == HexOrientation.PointyTop)
        {
            float q = (Mathf.Sqrt(3f) / 3f * p.x - 1f / 3f * p.y) / size;
            float r = (2f / 3f * p.y) / size;
            c = new CubeF(q, -q - r, r);
        }
        else
        {
            float q = (2f / 3f * p.x) / size;
            float r = (-1f / 3f * p.x + Mathf.Sqrt(3f) / 3f * p.y) / size;
            c = new CubeF(q, -q - r, r);
        }
        var rc = CubeRound(c);
        return new Axial((int)rc.x, (int)rc.z);
    }

    static Vector3 CubeRound(CubeF f)
    {
        float rx = Mathf.Round(f.x);
        float ry = Mathf.Round(f.y);
        float rz = Mathf.Round(f.z);

        float dx = Mathf.Abs(rx - f.x);
        float dy = Mathf.Abs(ry - f.y);
        float dz = Mathf.Abs(rz - f.z);

        if (dx > dy && dx > dz) rx = -ry - rz;
        else if (dy > dz) ry = -rx - rz;
        else rz = -rx - ry;

        return new Vector3(rx, ry, rz);
    }
    #endregion

    #region Geometry Helpers (lines, area, corners)
    public static int HexDistance(Axial a, Axial b)
    {
        return (Mathf.Abs(a.q - b.q) + Mathf.Abs(a.q + a.r - b.q - b.r) + Mathf.Abs(a.r - b.r)) / 2;
    }

    public static IEnumerable<Axial> HexLine(Axial from, Axial to)
    {
        int N = HexDistance(from, to);
        if (N < 1) { yield return from; yield break; }
        for (int i = 0; i <= N; i++)
        {
            float t = i / (float)N;
            var ac = new CubeF(from.q, -from.q - from.r, from.r);
            var bc = new CubeF(to.q, -to.q - to.r, to.r);
            var c = new CubeF(Mathf.Lerp(ac.x, bc.x, t), Mathf.Lerp(ac.y, bc.y, t), Mathf.Lerp(ac.z, bc.z, t));
            var r = CubeRound(c);
            yield return new Axial((int)r.x, (int)r.z);
        }
    }

    // Hex "rect": use a radius (disk) area
    public static IEnumerable<Axial> HexDisk(Axial center, int radius)
    {
        for (int dq = -radius; dq <= radius; dq++)
        {
            int rMin = Mathf.Max(-radius, -dq - radius);
            int rMax = Mathf.Min(radius, -dq + radius);
            for (int dr = rMin; dr <= rMax; dr++)
                yield return new Axial(center.q + dq, center.r + dr);
        }
    }

    public static void GetHexCorners(Vector2 center, float size, HexOrientation o, Vector3[] outVerts)
    {
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = (o == HexOrientation.PointyTop ? 60f * i - 30f : 60f * i);
            float rad = angleDeg * Mathf.Deg2Rad;
            outVerts[i] = new Vector3(center.x + size * Mathf.Cos(rad), center.y + size * Mathf.Sin(rad), 0f);
        }
    }
    #endregion
}