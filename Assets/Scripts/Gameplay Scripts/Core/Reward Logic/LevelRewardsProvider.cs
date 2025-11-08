using System;
using UnityEngine;

[DefaultExecutionOrder(-8)]
public class LevelRewardsProvider : MonoBehaviour
{
    #region Inspector
    [SerializeField, Tooltip("Provides CurrentLevelDefinition early in the frame.")]
    private LevelContextBinder contextBinder;

    [SerializeField, Tooltip("Fallback if the level has no reward definition.")]
    private LevelRewardDefinition fallbackRewards;
    #endregion

    #region State
    private LevelRewardDefinition current;
    #endregion

    #region Events
    /// <summary>Raised when Current rewards are resolved or change.</summary>
    public event Action<LevelRewardDefinition> OnRewardsAvailable;
    #endregion

    #region Public API
    public LevelRewardDefinition Current => current;

    /// <summary>Converts raw pickups to meta coins using the current rules.</summary>
    public int ToMetaCoins(int pickupCount)
    {
        return current != null ? current.ComputeCoinPayout(pickupCount) : pickupCount;
    }
    #endregion

    #region Unity
    private void Awake()
    {
        Resolve();
    }
    #endregion

    #region Private
    private void Resolve()
    {
        LevelRewardDefinition resolved = null;

        if (contextBinder != null && contextBinder.CurrentLevelDefinition != null)
        {
            resolved = contextBinder.CurrentLevelDefinition.RewardDefinition != null
                ? contextBinder.CurrentLevelDefinition.RewardDefinition
                : fallbackRewards;
        }
        else
        {
            resolved = fallbackRewards;
        }

        current = resolved;
        OnRewardsAvailable?.Invoke(current);
        Debug.Log(current != null
            ? $"[LevelRewardsProvider] Resolved rewards: coinValue={current.CoinValuePerPickup}, maxXP={current.MaxExpOnSuccess}"
            : "[LevelRewardsProvider] No rewards definition found; using raw pickups.");
    }
    #endregion
}
