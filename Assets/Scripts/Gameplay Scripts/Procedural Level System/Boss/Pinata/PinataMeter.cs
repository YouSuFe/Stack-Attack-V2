using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PinataMeter : MonoBehaviour
{
    #region Serialized
    [SerializeField, Tooltip("Points required for one payout cycle (e.g., 100).")]
    private int cycleThreshold = 100;

    [SerializeField, Tooltip("Optional global scale for incoming hit values.")]
    private float hitScale = 1f;

    [SerializeField, Tooltip("Clamp per-hit minimum. 0 = no clamp.")]
    private int minPerHit = 0;

    [SerializeField, Tooltip("Clamp per-hit maximum. 0 = no clamp.")]
    private int maxPerHit = 0;

    [SerializeField, Tooltip("If true, ignore hits when not enabled by director/controller.")]
    private bool gatedByController = true;
    #endregion

    #region Constants
    // Reward is granted every 70 total damage (global), independent of the 100-cycle UI.
    private const int REWARD_STEP = 70;
    #endregion

    #region Private
    private int currentPoints;       // progress within the current 0..Threshold-1 cycle
    private bool isEnabled;
    private int cycleCount;          // how many 100-cycles have been paid out
    private long totalDamage;        // cumulative total used for reward milestones (never reduced by cycle payouts)
    #endregion

    #region Actions
    public event Action<int, int> OnValueChanged;     // (current, threshold)
    public event Action<int, int> OnCyclePayout;      // (cycleIndex, remainder)
    public event Action<bool> OnPinataEnabledChanged; // enabled/disabled gate

    /// <summary>
    /// Fired when one or more rewards should be granted due to an applied hit.
    /// Argument: count (number of rewards to grant).
    /// </summary>
    public event Action<int> OnRewardsGranted;
    #endregion

    #region Properties
    public int Current => currentPoints;
    public int Threshold => Mathf.Max(1, cycleThreshold);
    public int CycleCount => cycleCount;
    public bool IsEnabled => isEnabled || !gatedByController;
    /// <summary>Total accumulated damage used for reward milestones.</summary>
    public long TotalDamage => totalDamage;
    #endregion

    #region Control
    /// <summary>Enable or disable the meter (gating damage intake and UI visibility via event).</summary>
    public void EnablePinata(bool enable)
    {
        isEnabled = enable;
        OnPinataEnabledChanged?.Invoke(IsEnabled);
    }

    /// <summary>
    /// Resets the current cycle progress. Optionally resets the cycle counter.
    /// (Does NOT reset TotalDamage; add a separate method if you need to clear global rewards.)
    /// </summary>
    public void ResetMeter(bool resetCycles = true)
    {
        currentPoints = 0;
        if (resetCycles) cycleCount = 0;
        OnValueChanged?.Invoke(currentPoints, Threshold);
    }
    #endregion

    #region Scoring
    /// <summary>
    /// Apply raw incoming damage (pre-scale). Handles:
    /// - Reward grants every 70 total (global, edge-case safe for huge bursts)
    /// - UI cycle payouts every 'Threshold' (typically 100), carrying remainder forward
    /// - Value change event for UI
    /// </summary>
    public void ApplyHit(int rawAmount)
    {
        if (!IsEnabled) return;

        // Scale & clamp
        int v = Mathf.RoundToInt(rawAmount * hitScale);
        if (minPerHit > 0 && v < minPerHit) v = minPerHit;
        if (maxPerHit > 0 && v > maxPerHit) v = maxPerHit;
        if (v <= 0) return;

        // ----- Rewards (every 70 total) -----
        long before = totalDamage;
        long after = before + v;
        int prevRewards = (int)(before / REWARD_STEP);
        int newRewards = (int)(after / REWARD_STEP);
        int granted = newRewards - prevRewards;
        totalDamage = after;

        if (granted > 0)
            OnRewardsGranted?.Invoke(granted);

        // ----- 100-cycle UI meter -----
        int threshold = Threshold;
        int previous = currentPoints;
        currentPoints += v;

        // Multiple payouts possible on big bursts; carry remainder
        while (currentPoints >= threshold)
        {
            currentPoints -= threshold;
            cycleCount++;
            OnCyclePayout?.Invoke(cycleCount, currentPoints); // remainder after payout
        }

        if (currentPoints != previous)
            OnValueChanged?.Invoke(currentPoints, threshold);
    }
    #endregion
}

