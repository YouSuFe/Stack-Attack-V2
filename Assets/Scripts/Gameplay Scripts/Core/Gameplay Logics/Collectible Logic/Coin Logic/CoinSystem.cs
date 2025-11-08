using System;
using UnityEngine;

/// <summary>
/// Scene-local coin pickup counter.
/// - Stores raw pickup count for the current run.
/// - Raises OnCoinsChanged for HUD updates.
/// - No per-level value logic here (kept in LevelRewardDefinition).
/// </summary>
public class CoinSystem : MonoBehaviour
{
    #region Singleton
    public static CoinSystem Instance { get; private set; }
    #endregion

    #region Inspector
    [Header("Coin Settings")]
    [SerializeField, Min(0), Tooltip("Starting pickups at scene start (usually 0).")]
    private int startingPickups = 0;
    #endregion

    #region State
    private int pickupCount = 0;
    #endregion

    #region Events
    /// <summary>Raised with the latest raw pickup count.</summary>
    public Action<int> OnCoinsChanged;
    #endregion

    #region Unity
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CoinSystem] Duplicate detected; destroying this component.");
            Destroy(this);
            return;
        }
        Instance = this;

        pickupCount = Mathf.Max(0, startingPickups);
        OnCoinsChanged?.Invoke(pickupCount);
    }
    #endregion

    #region Public API
    public int PickupCount => pickupCount;

    /// <summary>Add N pickups (usually +1 from a collectible).</summary>
    public void AddCoins(int amount)
    {
        if (amount == 0) return;
        pickupCount = Mathf.Max(0, pickupCount + amount);
        OnCoinsChanged?.Invoke(pickupCount);
    }

    public void AwardSinglePickup() => AddCoins(1);
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Add Pickup")] private void DebugAddPickup() => AwardSinglePickup();
    [ContextMenu("Debug/Add 10 Pickups")] private void DebugAdd10() => AddCoins(10);
#endif
}
