using System.Collections.Generic;
using UnityEngine;

public struct ShotCommand
{
    public float timeOffsetSeconds; // when to spawn, relative to trigger time
    public Vector3 localOffset;     // where to spawn relative to fire origin
    public float angleDegrees;      // rotation around Z (+Y = forward by convention)

    public ShotCommand(float t, Vector3 offset, float angleDeg)
    {
        timeOffsetSeconds = t;
        localOffset = offset;
        angleDegrees = angleDeg;
    }
}

public struct WeaponRuntimeStats
{
    public int projectileAmount;           // current amount (after upgrades)
    public int horizontalSimultaneousLimit;
    public OverflowResolution overflowResolution;
    public float sequentialShotIntervalSeconds;   // used by RapidStreak & FanSequential
    public float alternatingBurstIntervalSeconds; // used by AlternatingBurst
    public float maxFanAngleTotalDegrees;         // used by FanSequential

    public WeaponRuntimeStats(
        int amount,
        int limit,
        OverflowResolution overflow,
        float seq,
        float alt,
        float fan)
    {
        projectileAmount = amount;
        horizontalSimultaneousLimit = limit;
        overflowResolution = overflow;
        sequentialShotIntervalSeconds = seq;
        alternatingBurstIntervalSeconds = alt;
        maxFanAngleTotalDegrees = fan;
    }
}

public struct PatternContext
{
    public Transform fireOrigin;     // where to spawn relative offsets
    public float horizontalSpacing;  // world-units between neighbors in a row
    public float rowVerticalOffset;  // extra vertical delta between rows (if you use rows)

    // --- for alternating/missile patterns ---
    public Transform leftMuzzle;        // if null, pattern will use alternateSideOffsetX
    public Transform rightMuzzle;       // if null, pattern will use alternateSideOffsetX
    public float alternateSideOffsetX;  // fallback X offset if no muzzle transforms

    // Fan step angle (degrees) for Kunai
    public float fanStepDegrees;

    public PatternContext(
       Transform origin,
       float spacing,
       float rowYOffset,
       Transform left = null,
       Transform right = null,
       float altSideOffsetX = 0.6f,
       float fanStepDeg = 5f)
    {
        fireOrigin = origin;
        horizontalSpacing = spacing;
        rowVerticalOffset = rowYOffset;
        leftMuzzle = left;
        rightMuzzle = right;
        alternateSideOffsetX = altSideOffsetX;
        fanStepDegrees = fanStepDeg;
    }
}

public interface IShootPattern
{
    /// <summary>
    /// Produce a schedule of shot commands for this trigger.
    /// Returns a list sorted by time. The last command’s time defines the burst duration.
    /// </summary>
    List<ShotCommand> Generate(WeaponRuntimeStats stats, PatternContext ctx);
}
