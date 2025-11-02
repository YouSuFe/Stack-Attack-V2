using System;
using UnityEngine;

/// <summary>
/// Central EXP + Level-Up controller for roguelike-style upgrades.
/// - Each enemy kill = 1 EXP (added via AddExp or EnemyExpOnDeath).
/// - Overflow ALWAYS carries to next level.
/// - Thresholds follow an arithmetic sequence: baseThreshold + stepIncrement * upgradesTaken
///   e.g., 4, 10, 16, 22, ...
/// - Optionally opens PowerupPanel on level-up (if reference assigned).
/// </summary>
public class ExperienceSystem : MonoBehaviour
{
    #region Singleton
    public static ExperienceSystem Instance { get; private set; }
    #endregion

    #region Inspector
    [Header("Progression (Arithmetic Sequence)")]
    [Tooltip("First threshold (EXP needed for the first upgrade). Example: 4.")]
    [SerializeField, Min(1)] private int baseThreshold = 4;

    [Tooltip("Constant step added each level. Example: 6 -> 4,10,16,22,...")]
    [SerializeField, Min(0)] private int stepIncrement = 6;

    [Header("Optional: Auto-Show Powerup Panel on Level-Up")]
    [SerializeField] private PowerupPanelUIController powerupPanel;
    #endregion

    #region State (not exposed to Inspector)
    private int currentExp = 0;
    private int upgradesTaken = 0; // number of level-ups already achieved
    private int nextThreshold = 4;
    #endregion

    #region Events
    public Action<int, int> OnExpChanged; // (currentExp, nextThreshold)
    public Action<int> OnLeveledUp;       // (upgradesTaken after increment)
    #endregion

    #region Unity
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ExperienceSystem] Duplicate detected; destroying this component.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        nextThreshold = ComputeThresholdForNextUpgrade(upgradesTaken);
        currentExp = 0;
        if (OnExpChanged != null)
            OnExpChanged(currentExp, nextThreshold);
    }
    #endregion

    #region Public API
    /// <summary>Add EXP (positive integers expected). Overflow carries.</summary>
    public void AddExp(int amount)
    {
        if (amount == 0) return;
        currentExp = Mathf.Max(0, currentExp + amount);
        ResolveLevelUpsIfAny();

        if (OnExpChanged != null) OnExpChanged(currentExp, nextThreshold);
    }

    /// <summary>
    /// Returns the next threshold given how many upgrades have already been taken,
    /// using an arithmetic progression: base + n * step.
    /// </summary>
    public int ComputeThresholdForNextUpgrade(int upgradesAlreadyTaken)
    {
        long baseVal = Mathf.Max(1, baseThreshold);
        long step = Mathf.Max(0, stepIncrement);
        long n = Mathf.Max(0, upgradesAlreadyTaken);
        long value = baseVal + step * n;
        return (int)Mathf.Clamp(value, 1, int.MaxValue);
    }

    public int CurrentExp => currentExp;
    public int NextThreshold => nextThreshold;
    public int UpgradesTaken => upgradesTaken;
    #endregion

    #region Internal
    private void ResolveLevelUpsIfAny()
    {
        // Handle burst kills (multiple level-ups in one frame)
        int safety = 256; // guard against bad data
        while (currentExp >= nextThreshold && safety-- > 0)
        {
            currentExp -= nextThreshold; // carry remaining EXP
            upgradesTaken++;
            nextThreshold = ComputeThresholdForNextUpgrade(upgradesTaken);

            if (OnLeveledUp != null) OnLeveledUp(upgradesTaken);

            if (powerupPanel != null)
            {
                if (PauseManager.Instance != null)
                    PauseManager.Instance.StopGameplay();
                powerupPanel.ShowAndRoll();
            }
        }

        if (safety <= 0)
            Debug.LogError("[ExperienceSystem] ResolveLevelUpsIfAny hit safety limit. Check threshold values.");
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Add 1 EXP")] private void DebugAdd1() => AddExp(1);
    [ContextMenu("Debug/Add 7 EXP")] private void DebugAdd7() => AddExp(7);
    [ContextMenu("Debug/Force Threshold")] private void DebugToThreshold() => AddExp(nextThreshold - currentExp);
#endif
}
