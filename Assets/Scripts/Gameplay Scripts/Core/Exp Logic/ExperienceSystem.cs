using System;
using UnityEngine;

/// <summary>
/// Central EXP + Level-Up controller for roguelike-style upgrades.
/// - Each enemy kill = 1 EXP (added via AddExp or EnemyExpOnDeath).
/// - Overflow ALWAYS carries to next level.
/// - Thresholds follow an arithmetic sequence: baseThreshold + stepIncrement * upgradesTaken
///   e.g., 4, 10, 16, 22, ...
/// - Decoupled: no UI references. Orchestrator handles panel & pause.
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
    #endregion

    #region State (not exposed to Inspector)
    private int currentExp = 0;
    private int upgradesTaken = 0;    // number of level-ups already achieved
    private int nextThreshold = 4;

    // Number of level-ups waiting for player choices (drained by orchestrator)
    private int pendingUpgrades = 0;
    #endregion


    public int CurrentExp => currentExp;
    public int NextThreshold => nextThreshold;
    public int UpgradesTaken => upgradesTaken;
    public int PendingUpgrades => pendingUpgrades;

    #region Events
    public Action<int, int> OnExpChanged;            // (currentExp, nextThreshold)
    public Action<int> OnLeveledUp;                  // (upgradesTaken after increment)
    public Action<int> OnPendingUpgradesChanged;     // (pendingUpgrades)
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

        OnExpChanged?.Invoke(currentExp, nextThreshold);
        OnPendingUpgradesChanged?.Invoke(pendingUpgrades);
    }
    #endregion

    #region Public API
    /// <summary>Add EXP (positive integers expected). Overflow carries.</summary>
    public void AddExp(int amount)
    {
        if (amount == 0) return;

        currentExp = Mathf.Max(0, currentExp + amount);
        ResolveLevelUpsIfAny();

        // match original pattern: notify after resolution
        OnExpChanged?.Invoke(currentExp, nextThreshold);
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


    /// <summary>
    /// Consume exactly one pending upgrade (called by orchestrator after a card is applied).
    /// Returns true if one was consumed.
    /// </summary>
    public bool TryConsumeOnePendingUpgrade()
    {
        if (pendingUpgrades <= 0) return false;
        pendingUpgrades--;
        OnPendingUpgradesChanged?.Invoke(pendingUpgrades);
        return true;
    }
    #endregion

    #region Internal
    private void ResolveLevelUpsIfAny()
    {
        // Handle burst kills (multiple level-ups in one frame)
        int safety = 256; // guard against bad data
        int leveledThisCall = 0;

        while (currentExp >= nextThreshold && safety-- > 0)
        {
            currentExp -= nextThreshold; // carry remaining EXP
            upgradesTaken++;
            nextThreshold = ComputeThresholdForNextUpgrade(upgradesTaken);

            OnLeveledUp?.Invoke(upgradesTaken);

            // Queue for UI resolution (orchestrator will open panel and drain)
            pendingUpgrades++;
            leveledThisCall++;
        }

        if (leveledThisCall > 0)
            OnPendingUpgradesChanged?.Invoke(pendingUpgrades);

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
