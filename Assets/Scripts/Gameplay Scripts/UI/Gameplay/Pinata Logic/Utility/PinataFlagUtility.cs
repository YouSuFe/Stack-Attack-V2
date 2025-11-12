using UnityEngine;
/// <summary>
/// Utility for computing per-cycle reward flag positions on the pinata UI.
/// Flags represent milestones at multiples of 'rewardStep' within the current 0..threshold cycle.
/// Special rule: if a milestone lands exactly on the cycle start (normalized 0),
/// it should render at the END of the bar (normalized 1).
/// </summary>
public static class PinataFlagUtility
{
    /// <summary>
    /// Compute normalized positions (0..1] of reward milestones (k * rewardStep) that fall
    /// within the current 100-cycle interval [cycleStart, cycleEnd).
    /// - Typically returns 1 or 2 entries (because 70 steps within 100 can yield up to two per cycle).
    /// - If a milestone lands exactly at the cycle start boundary, it returns 1.0 (end of bar), not 0.0.
    /// </summary>
    /// <param name="totalDamage">Global accumulated damage.</param>
    /// <param name="threshold">Cycle size (e.g., 100). Clamped to at least 1.</param>
    /// <param name="rewardStep">Reward step size (e.g., 70). Must be >= 1.</param>
    /// <param name="outPositions">Destination array (e.g., length 2). Will be filled from index 0 up.</param>
    /// <returns>Number of valid positions written (0..outPositions.Length).</returns>
    public static int GetCurrentCycleFlagPositions(long totalDamage, int threshold, int rewardStep, float[] outPositions)
    {
        if (outPositions == null || outPositions.Length == 0) return 0;
        if (rewardStep < 1) rewardStep = 1;
        threshold = Mathf.Max(1, threshold);

        // Current cycle half-open interval [cycleStart, cycleEnd)
        long cycleStart = (totalDamage / threshold) * threshold;
        long cycleEnd = cycleStart + threshold;

        // First k such that k*rewardStep >= cycleStart  (ceil division)
        long kStart = (cycleStart + rewardStep - 1) / rewardStep;

        int count = 0;
        for (long k = kStart; ; k++)
        {
            long milestone = k * rewardStep;
            if (milestone >= cycleEnd) break;

            // Compute normalized position within this cycle.
            // Boundary rule: milestone == cycleStart -> place at 1.0 (end of bar), not 0.0 (start).
            float normalized = (milestone == cycleStart)
                ? 1f
                : (float)(milestone - cycleStart) / threshold; // strictly (0,1) for non-boundary

            outPositions[count] = normalized;
            count++;

            if (count >= outPositions.Length) break; // caller controls max flags (typically 2)
        }

        return count;
    }
}
