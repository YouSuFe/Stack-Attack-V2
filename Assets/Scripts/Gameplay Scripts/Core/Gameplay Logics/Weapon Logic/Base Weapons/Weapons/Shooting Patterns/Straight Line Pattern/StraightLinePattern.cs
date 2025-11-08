using System.Collections.Generic;
using UnityEngine;

public class StraightLinePattern : IShootPattern
{
    public List<ShotCommand> Generate(WeaponRuntimeStats stats, PatternContext ctx)
    {
        var result = new List<ShotCommand>(stats.projectileAmount);

        int amount = Mathf.Max(1, stats.projectileAmount);
        int limit = Mathf.Max(1, stats.horizontalSimultaneousLimit);

        // How many can we spawn simultaneously at t=0 in the main row
        int simultaneous = Mathf.Min(amount, limit);
        int overflow = Mathf.Max(0, amount - simultaneous);

        // Helper: add a balanced row with odd/even centered spacing
        void AddBalancedRow(int count, float t, float extraY)
        {
            if (count <= 0) return;

            // ODD: 0, +1, -1, +2, -2 ...
            // EVEN: -0.5, +0.5, -1.5, +1.5, ...
            bool isOdd = (count % 2) == 1;

            if (isOdd)
            {
                // center first
                result.Add(new ShotCommand(t, new Vector3(0f, extraY, 0f), 0f));

                int placed = 1;
                int stepIndex = 1;
                while (placed < count)
                {
                    float xPos = stepIndex * ctx.horizontalSpacing;
                    // +x
                    if (placed < count)
                    {
                        result.Add(new ShotCommand(t, new Vector3(+xPos, extraY, 0f), 0f));
                        placed++;
                    }
                    // -x
                    if (placed < count)
                    {
                        result.Add(new ShotCommand(t, new Vector3(-xPos, extraY, 0f), 0f));
                        placed++;
                    }
                    stepIndex++;
                }
            }
            else
            {
                // first pair at ±0.5 spacing
                int placed = 0;
                float half = 0.5f;
                int stepIndex = 0;

                while (placed < count)
                {
                    float baseHalf = half + stepIndex; // 0.5, 1.5, 2.5, ...
                    float xRight = +baseHalf * ctx.horizontalSpacing;
                    float xLeft = -baseHalf * ctx.horizontalSpacing;

                    // Left then Right (or swap if you prefer)
                    result.Add(new ShotCommand(t, new Vector3(xLeft, extraY, 0f), 0f));
                    placed++;
                    if (placed < count)
                    {
                        result.Add(new ShotCommand(t, new Vector3(xRight, extraY, 0f), 0f));
                        placed++;
                    }
                    stepIndex++;
                }
            }
        }

        // If we’re in Rows mode and there *is* overflow,
        // put the MAIN row "in front" (positive Y = forward) using ctx.rowVerticalOffset,
        // and place the overflow row at the original Y (0).
        if (overflow > 0 && stats.overflowResolution == OverflowResolution.Rows)
        {
            // main row ahead
            AddBalancedRow(simultaneous, 0f, ctx.rowVerticalOffset);
            // overflow row at base plane
            AddBalancedRow(overflow, 0f, 0f);
            return result;
        }

        // No overflow or using RapidStreak → original placement
        AddBalancedRow(simultaneous, 0f, 0f);

        if (overflow <= 0) return result;

        // RapidStreak: emit overflow sequentially (unchanged)
        float t = stats.sequentialShotIntervalSeconds;
        for (int i = 0; i < overflow; i++)
        {
            result.Add(new ShotCommand(t, Vector3.zero, 0f));
            t += stats.sequentialShotIntervalSeconds;
        }

        return result;
    }
}
