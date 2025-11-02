using System;
using UnityEngine;

/// <summary>
/// Scene-local coin tracker.
/// - Holds total coin count (int).
/// - Coin value per pickup is set in the inspector on the CoinSystem (default 1).
/// - Raises OnCoinsChanged when total updates.
/// </summary>
public class CoinSystem : MonoBehaviour
{
    #region Singleton
    public static CoinSystem Instance { get; private set; }
    #endregion

    #region Inspector
    [Header("Coin Settings")]
    [Tooltip("How many coins a single pickup grants. Integer, default 1.")]
    [SerializeField, Min(0)] private int coinValuePerPickup = 1;

    [Tooltip("Starting coins at scene start.")]
    [SerializeField, Min(0)] private int startingCoins = 0;
    #endregion

    #region State
    private int totalCoins = 0;
    #endregion

    #region Events
    /// <summary>Raised whenever total coin amount changes.</summary>
    public Action<int> OnCoinsChanged;
    #endregion

    #region Unity
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CoinSystem] Duplicate detected; destroying this component.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        totalCoins = Mathf.Max(0, startingCoins);
        if (OnCoinsChanged != null) OnCoinsChanged(totalCoins);
    }
    #endregion

    #region Public API
    /// <summary>Value of a single coin pickup (from inspector).</summary>
    public int CoinValuePerPickup => coinValuePerPickup;

    /// <summary>Current total coins.</summary>
    public int TotalCoins => totalCoins;

    /// <summary>Add coins and notify listeners.</summary>
    public void AddCoins(int amount)
    {
        if (amount == 0) return;
        totalCoins = Mathf.Max(0, totalCoins + amount);
        if (OnCoinsChanged != null) OnCoinsChanged(totalCoins);
    }

    /// <summary>Award one coin pickup using the configured per-pickup value.</summary>
    public void AwardSinglePickup()
    {
        AddCoins(coinValuePerPickup);
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Add Pickup Value")] private void DebugAddPickup() => AwardSinglePickup();
    [ContextMenu("Debug/Add 10 Coins")] private void DebugAdd10() => AddCoins(10);
#endif
}