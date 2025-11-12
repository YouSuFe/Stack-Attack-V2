using UnityEngine;

public static class PinataFlagUtility
{
    /// <summary>
    /// Compute normalized positions (0..1) of reward milestones (k * rewardStep) that fall
    /// strictly inside the current 100-cycle interval (cycleStart, cycleEnd). We EXCLUDE the
    /// milestone at cycleStart (carry-in from previous cycle), so cycles with only one real
    /// milestone will show a single flag.
    ///
    /// Examples with threshold=100, rewardStep=70:
    ///   0–100:   only 70   → [0.7]
    ///   100–200: only 140  → [0.4]
    ///   200–300: 210, 280  → [0.1, 0.8]
    ///   700–800: only 770  → [0.7]   (700 is excluded because it's the cycleStart)
    /// </summary>
    public static int GetCurrentCycleFlagPositions(long totalDamage, int threshold, int rewardStep, float[] outPositions)
    {
        if (outPositions == null || outPositions.Length == 0) return 0;
        if (rewardStep < 1) rewardStep = 1;
        threshold = Mathf.Max(1, threshold);

        long cycleStart = (totalDamage / threshold) * threshold;
        long cycleEnd = cycleStart + threshold;

        // First k such that k*rewardStep > cycleStart (STRICTLY greater than start).
        long k = (cycleStart / rewardStep) + 1;

        int count = 0;
        for (; ; k++)
        {
            long milestone = k * rewardStep;
            if (milestone >= cycleEnd) break; // half-open interval (cycleStart, cycleEnd)

            float normalized = (float)(milestone - cycleStart) / threshold; // (0,1)
            outPositions[count] = normalized;
            count++;

            if (count >= outPositions.Length) break; // at most 2 per 100 with step 70
        }

        return count;
    }
}
