using UnityEngine;

public static class WeaponPatternFactory
{
    private static readonly StraightLinePattern straightLine = new StraightLinePattern();
    private static readonly AlternatingBurstPattern alternating = new AlternatingBurstPattern();
     private static readonly FanSequentialPattern fan = new FanSequentialPattern();

    public static IShootPattern Get(WeaponPatternType type)
    {
        switch (type)
        {
            case WeaponPatternType.StraightLine: return straightLine;
            case WeaponPatternType.AlternatingBurst: return alternating;
            case WeaponPatternType.FanSequential:  return fan;
            default:
                Debug.LogWarning($"WeaponPatternFactory: Unhandled type {type}, fallback to StraightLine.");
                return straightLine;
        }
    }
}

