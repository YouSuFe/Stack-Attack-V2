using System.Collections.Generic;
using UnityEngine;

public class AlternatingBurstPattern : IShootPattern
{
    public List<ShotCommand> Generate(WeaponRuntimeStats stats, PatternContext ctx)
    {
        var result = new List<ShotCommand>(stats.projectileAmount);

        float t = 0f;
        bool nextLeft = true;
        float step = Mathf.Max(0f, stats.alternatingBurstIntervalSeconds);

        for (int i = 0; i < stats.projectileAmount; i++)
        {
            Vector3 localOffset;

            if (ctx.leftMuzzle != null && ctx.rightMuzzle != null)
            {
                // Convert world muzzle to local offset relative to fireOrigin
                localOffset = nextLeft
                    ? ctx.fireOrigin.InverseTransformPoint(ctx.leftMuzzle.position)
                    : ctx.fireOrigin.InverseTransformPoint(ctx.rightMuzzle.position);
            }
            else
            {
                // Fallback: symmetric X offsets around origin
                float x = nextLeft ? -ctx.alternateSideOffsetX : ctx.alternateSideOffsetX;
                localOffset = new Vector3(x, 0f, 0f);
            }

            result.Add(new ShotCommand(t, localOffset, 0f));
            nextLeft = !nextLeft;
            t += step;
        }

        return result;
    }
}
