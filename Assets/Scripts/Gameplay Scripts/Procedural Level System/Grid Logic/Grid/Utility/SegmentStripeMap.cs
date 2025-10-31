using System;
using System.Collections.Generic;
using UnityEngine;

public struct StripeInfo
{
    public int segmentLocalStartRow;   // inclusive
    public int segmentLocalEndRow;     // exclusive
    public int worldStartRow;          // segmentBaseWorldRow + segmentLocalStartRow
    public int worldEndRow;            // worldStartRow + rows
    public float yBase;                // world Y where stripe-local row 0 sits (includes gaps)
    public GridTopology topology;
    public GridConfig config;
}

public static class SegmentStripeMap
{
    /// <summary>
    /// Build a stripe map for one segment. If the segment has no stripes,
    /// we return a single stripe that covers the whole segment using levelDefaultConfig.
    /// </summary>
    public static List<StripeInfo> Build(
    LevelSegment segment,
    int segmentBaseWorldRow,             // world-row index where this segment begins (from painter's RecomputeSegmentBases)
    GridConfig defaultConfig,            // used when a stripe has no override or when there are no stripes
    System.Func<GridConfig, float> RowStepY // callback: returns world Y step per row for the given config
)
    {
        var result = new List<StripeInfo>(4);

        if (segment == null)
            return result;

        int segLen = Mathf.Max(0, segment.LengthInRows);

        // --- Helper to append one stripe with exact indexing + y stacking
        void AddStripe(GridConfig cfg, int rows, float yBase, int localStartRow, int worldStartRow)
        {
            if (rows <= 0) return;

            var s = new StripeInfo
            {
                topology = cfg.Topology,
                config = cfg,
                yBase = yBase,                                   // world Y start of this stripe (used by GridStripeAdapter)
                worldStartRow = worldStartRow,                           // inclusive
                worldEndRow = worldStartRow + rows,                    // exclusive
                segmentLocalStartRow = localStartRow,                           // inclusive
                segmentLocalEndRow = localStartRow + rows                     // exclusive
            };
            result.Add(s);
        }

        // Base world Y of THIS segment = (segmentBaseWorldRow * default step).
        // This matches how bands were previously approximated and gives a stable 0-origin for seg 0.
        // Inside the segment we stack stripes using EACH stripe's own step + yGap to avoid overlaps.
        float defaultStep = RowStepY(defaultConfig);
        float runningY = defaultConfig.OriginY + segmentBaseWorldRow * defaultStep;

        int localCursor = 0;                  // segment-local row pointer
        int worldCursor = segmentBaseWorldRow;

        // If you have per-segment stripes defined, stack them in order.
        var stripes = (segment.Stripes != null) ? segment.Stripes : null;

        if (stripes != null && stripes.Count > 0)
        {
            for (int i = 0; i < stripes.Count && localCursor < segLen; i++)
            {
                var def = stripes[i];
                var cfg = def.config != null ? def.config : defaultConfig;
                int rows = Mathf.Clamp(def.rows, 0, segLen - localCursor);
                float step = RowStepY(cfg);

                // Insert this stripe
                AddStripe(cfg, rows, runningY, localCursor, worldCursor);

                // Advance exact Y & row cursors by this stripe's own height + gap
                runningY += rows * step + Mathf.Max(0f, def.gapBeforeY);
                localCursor += rows;
                worldCursor += rows;
            }
        }

        // If the stripes didn't cover the full segment length, fill the remainder as one default stripe.
        int remaining = segLen - localCursor;
        if (remaining > 0)
        {
            float step = RowStepY(defaultConfig);
            AddStripe(defaultConfig, remaining, runningY, localCursor, worldCursor);

            // (advance not strictly required after the last add, but kept for clarity)
            runningY += remaining * step;
            localCursor += remaining;
            worldCursor += remaining;
        }

        return result;
    }


    /// <summary>
    /// Find which stripe contains a given worldRow. Returns stripe index and the stripe-local row.
    /// </summary>
    public static bool TryFindByWorldRow(IReadOnlyList<StripeInfo> stripes, int worldRow, out int stripeIndex, out int stripeLocalRow)
    {
        stripeIndex = -1;
        stripeLocalRow = -1;
        if (stripes == null) return false;

        for (int i = 0; i < stripes.Count; i++)
        {
            var s = stripes[i];
            if (worldRow >= s.worldStartRow && worldRow < s.worldEndRow)
            {
                stripeIndex = i;
                stripeLocalRow = worldRow - s.worldStartRow;
                return true;
            }
        }
        return false;
    }
}