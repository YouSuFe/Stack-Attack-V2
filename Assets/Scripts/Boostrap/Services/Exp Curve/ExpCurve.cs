using UnityEngine;

[CreateAssetMenu(menuName = "Progression/Exp Curve", fileName = "ExpCurve")]
public class ExpCurve : ScriptableObject
{
    [Tooltip("Cumulative EXP required to REACH each next level.\n" +
             "Example: [4,9,15] ⇒ L1→L2 at 4 total, L2→L3 at 9 total, L3→L4 at 15 total.")]
    public int[] cumulativeToLevel; // length = maxLevel-1 (since Level 1 requires 0)

    /// <summary>Returns total EXP required to reach the given level (level>=2). For level 1 returns 0.</summary>
    public int GetCumulativeForLevel(int level)
    {
        if (level <= 1 || cumulativeToLevel == null || cumulativeToLevel.Length == 0) return 0;
        int idx = Mathf.Clamp(level - 2, 0, cumulativeToLevel.Length - 1);
        return Mathf.Max(0, cumulativeToLevel[idx]);
    }

    /// <summary>Highest attainable level with this curve (Level 1 + number of thresholds).</summary>
    public int MaxLevel => 1 + Mathf.Max(0, cumulativeToLevel?.Length ?? 0);
}
