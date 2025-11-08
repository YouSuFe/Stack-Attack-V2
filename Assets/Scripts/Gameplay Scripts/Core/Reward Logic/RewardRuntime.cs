using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes end-of-run rewards using LevelRewardsProvider + CoinSystem + LevelProgressRuntime.
/// Also supports an optional override progress % for debug.
/// </summary>
[DefaultExecutionOrder(-5)]
public class RewardRuntime : MonoBehaviour
{
    #region Inspector
    [SerializeField, Tooltip("Provides level reward rules (coin value, XP).")]
    private LevelRewardsProvider rewardsProvider;

    [SerializeField, Tooltip("Provides reached % for XP calculation.")]
    private LevelProgressRuntime progressRuntime;

    [Header("Icons (optional)")]
    [SerializeField] private Sprite coinIcon;
    [SerializeField] private Sprite xpIcon;
    #endregion

    #region State
    private readonly List<ResultViewBase.RewardEntry> lastBuiltEntries = new List<ResultViewBase.RewardEntry>(2);
    private int lastBuiltReachedPercent;
    #endregion

    #region Public API
    public IReadOnlyList<ResultViewBase.RewardEntry> LastEntries => lastBuiltEntries;
    public int LastReachedPercent => lastBuiltReachedPercent;

    /// <summary>
    /// Build rewards immediately using current state.
    /// Optionally pass an overrideReachedPercent to test without real progress.
    /// </summary>
    public void BuildNow(bool success, int? overrideReachedPercent = null)
    {
        lastBuiltEntries.Clear();

        int pickups = CoinSystem.Instance != null ? CoinSystem.Instance.PickupCount : 0;
        int coinsMeta = rewardsProvider != null ? rewardsProvider.ToMetaCoins(pickups) : pickups;

        int reachedPercent = overrideReachedPercent ??
                             (progressRuntime != null ? progressRuntime.ProgressPercent : (success ? 100 : 0));

        int xp = 0;
        if (rewardsProvider != null && rewardsProvider.Current != null)
            xp = rewardsProvider.Current.ComputeXpAward(success, reachedPercent);

        if (coinsMeta > 0)
            lastBuiltEntries.Add(new ResultViewBase.RewardEntry { id = "coin", amount = coinsMeta, icon = coinIcon });
        if (xp > 0)
            lastBuiltEntries.Add(new ResultViewBase.RewardEntry { id = "xp", amount = xp, icon = xpIcon });

        lastBuiltReachedPercent = reachedPercent;

        Debug.Log($"[RewardRuntime] Built rewards (success={success}) -> coins={coinsMeta}, xp={xp}, reached={reachedPercent}%");
    }
    #endregion

#if UNITY_EDITOR
    #region Editor Debug Helpers
    [Header("Debug (Editor Only)")]
    [SerializeField, Tooltip("When true, editor debug buttons will use this % instead of runtime progress.")]
    private bool useDebugReachedPercent = false;

    [SerializeField, Range(0, 100), Tooltip("Debug reached percent used when override is enabled.")]
    private int debugReachedPercent = 50;

    private int? GetOverridePercentOrNull() => useDebugReachedPercent ? (int?)debugReachedPercent : null;

    [ContextMenu("Debug/Build Success (Use Override % if set)")]
    private void DebugBuildSuccessOnly()
    {
        BuildNow(true, GetOverridePercentOrNull());
        Debug.Log($"[RewardRuntime] Entries: {FormatEntriesForLog()}");
    }

    [ContextMenu("Debug/Build Failure (Use Override % if set)")]
    private void DebugBuildFailureOnly()
    {
        BuildNow(false, GetOverridePercentOrNull());
        Debug.Log($"[RewardRuntime] Entries: {FormatEntriesForLog()}");
    }

    private string FormatEntriesForLog()
    {
        if (lastBuiltEntries.Count == 0) return "(none)";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < lastBuiltEntries.Count; i++)
        {
            var e = lastBuiltEntries[i];
            sb.Append($"[{i}] {e.id} +{e.amount}  ");
        }
        return sb.ToString();
    }
    #endregion
#endif
}
