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

    #region Private
    private int currentPoints;
    private bool isEnabled;
    private int cycleCount;
    #endregion

    #region Actions
    public event Action<int, int> OnValueChanged;   // (current, threshold)
    public event Action<int, int> OnCyclePayout;    // (cycleIndex, remainder)
    public event Action<bool> OnPinataEnabledChanged;
    #endregion

    #region Properties
    public int Current => currentPoints;
    public int Threshold => Mathf.Max(1, cycleThreshold);
    public int CycleCount => cycleCount;
    public bool IsEnabled => isEnabled || !gatedByController;
    #endregion

    #region Control
    public void EnablePinata(bool enable)
    {
        isEnabled = enable;
        OnPinataEnabledChanged?.Invoke(IsEnabled);
    }

    public void ResetMeter(bool resetCycles = true)
    {
        currentPoints = 0;
        if (resetCycles) cycleCount = 0;
        OnValueChanged?.Invoke(currentPoints, Threshold);
    }
    #endregion

    #region Scoring
    public void ApplyHit(int rawAmount)
    {
        if (!IsEnabled) return;

        int v = Mathf.RoundToInt(rawAmount * hitScale);
        if (minPerHit > 0 && v < minPerHit) v = minPerHit;
        if (maxPerHit > 0 && v > maxPerHit) v = maxPerHit;
        if (v <= 0) return;

        currentPoints += v;

        int threshold = Threshold;
        while (currentPoints >= threshold)
        {
            currentPoints -= threshold;
            cycleCount++;
            OnCyclePayout?.Invoke(cycleCount, currentPoints);
        }

        OnValueChanged?.Invoke(currentPoints, threshold);
    }
    #endregion
}
