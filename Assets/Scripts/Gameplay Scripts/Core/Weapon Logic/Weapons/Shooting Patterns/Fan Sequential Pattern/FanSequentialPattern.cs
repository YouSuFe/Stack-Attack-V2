using System.Collections.Generic;
using UnityEngine;

public class FanSequentialPattern : IShootPattern
{
    public List<ShotCommand> Generate(WeaponRuntimeStats stats, PatternContext ctx)
    {
        var result = new List<ShotCommand>(stats.projectileAmount);

        int total = Mathf.Max(1, stats.projectileAmount);
        float halfCone = Mathf.Max(0f, stats.maxFanAngleTotalDegrees * 0.5f);   // e.g., 15°
        float step = Mathf.Max(0.01f, ctx.fanStepDegrees);                      // e.g., 5°
        float delay = Mathf.Max(0f, stats.sequentialShotIntervalSeconds);

        // Build the “capacity” list of angles within the cone using center-first ordering
        // Center-first order for nice presentation: 0, +step, -step, +2step, -2step...
        List<float> centerOrderAngles = new List<float>();
        centerOrderAngles.Add(0f);

        for (float a = step; a <= halfCone + 0.0001f; a += step)
        {
            centerOrderAngles.Add(+a);
            centerOrderAngles.Add(-a);
        }

        // Also build a sweep order list (rightmost to leftmost)
        // “Rightmost” we’ll define as negative angles (clockwise), then up to positive
        List<float> sweepAngles = new List<float>();
        for (float a = -halfCone; a <= halfCone + 0.0001f; a += step)
            sweepAngles.Add(a);

        float t = 0f;

        if (total <= centerOrderAngles.Count)
        {
            // We can fit all shots within the cone once using center-first order
            for (int i = 0; i < total; i++)
            {
                float ang = centerOrderAngles[i];
                result.Add(new ShotCommand(t, Vector3.zero, ang));
                t += delay;
            }
        }
        else
        {
            // We exceed the cone capacity → use sweeping passes (right → left) until we reach total
            int emitted = 0;

            // First pass: emit one center-first cycle up to capacity (looks nice at start)
            int firstBurst = Mathf.Min(total, centerOrderAngles.Count);
            for (int i = 0; i < firstBurst; i++)
            {
                float ang = centerOrderAngles[i];
                result.Add(new ShotCommand(t, Vector3.zero, ang));
                t += delay;
                emitted++;
            }

            // Remaining: sweep across the cone repeatedly
            while (emitted < total)
            {
                for (int i = 0; i < sweepAngles.Count && emitted < total; i++)
                {
                    float ang = sweepAngles[i];
                    result.Add(new ShotCommand(t, Vector3.zero, ang));
                    t += delay;
                    emitted++;
                }
            }
        }

        return result;
    }
}

