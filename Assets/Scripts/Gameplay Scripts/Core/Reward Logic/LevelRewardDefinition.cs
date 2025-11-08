using UnityEngine;

/// <summary>
/// Per-level reward tuning.
/// - Holds coin value per pickup.
/// - Holds XP caps/floors.
/// - Provides conversion helpers used by HUD and end-of-run logic.
/// </summary>
[CreateAssetMenu(menuName = "Game/Rewards/Level Reward Definition", fileName = "LevelRewardDefinition")]
public class LevelRewardDefinition : ScriptableObject
{
    #region Private Fields
    [Header("Coin")]
    [SerializeField, Tooltip("Meta-currency gained per coin pickup on this level.")]
    private int coinValuePerPickup = 1;

    [Header("XP")]
    [SerializeField, Tooltip("Max XP at 100% completion on success.")]
    private int maxExpOnSuccess = 8;

    [SerializeField, Tooltip("Minimum XP floor on failure (for early deaths).")]
    private int minExpOnFail = 1;
    #endregion

    #region Public Getters
    public int CoinValuePerPickup => Mathf.Max(0, coinValuePerPickup);
    public int MaxExpOnSuccess => Mathf.Max(0, maxExpOnSuccess);
    public int MinExpOnFail => Mathf.Max(0, minExpOnFail);
    #endregion

    #region Public Methods
    /// <summary>Converts raw pickup count to valued meta coins for this level.</summary>
    public int ComputeCoinPayout(int pickupCount)
    {
        pickupCount = Mathf.Max(0, pickupCount);
        return pickupCount * CoinValuePerPickup;
    }

    /// <summary>
    /// Computes XP award from completion percent (0..100).
    /// Uses CeilToInt scaling by completion; floors to MinExpOnFail on failure.
    /// </summary>
    public int ComputeXpAward(bool success, int reachedPercent)
    {
        int clamped = Mathf.Clamp(reachedPercent, 0, 100);
        float t = clamped / 100f;
        int xp = Mathf.CeilToInt(MaxExpOnSuccess * t);
        return success
            ? Mathf.Clamp(xp, 0, MaxExpOnSuccess)
            : Mathf.Clamp(Mathf.Max(xp, MinExpOnFail), MinExpOnFail, MaxExpOnSuccess);
    }
    #endregion
}
