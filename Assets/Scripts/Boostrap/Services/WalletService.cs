using UnityEngine;
using System;

/// <summary>
/// WalletService
/// Persistent runtime authority for the player's coin balance.
/// - Automatically loads from PlayerPrefs on Awake.
/// - Automatically saves on ApplicationQuit.
/// - Provides add/spend/set APIs and notifies listeners via OnCoinsChanged.
/// </summary>
[DefaultExecutionOrder(-50)]
public class WalletService : MonoBehaviour
{
    #region Constants
    private const string PREF_COINS = "WALLET_TotalCoins";
    #endregion

    #region Singleton
    public static WalletService Instance { get; private set; }
    #endregion

    #region Serialized Fields
    [SerializeField, Tooltip("Starting coin amount when no save exists.")]
    private int startingCoins = 0;
    #endregion

    #region Private Fields
    private int coins;
    #endregion

    #region Events
    public event Action<int> OnCoinsChanged;
    #endregion

    #region Public Properties
    public int Coins => coins;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Load();
    }

    private void OnApplicationQuit()
    {
        Save();
    }
    #endregion

    #region Public API
    /// <summary>Adds coins and saves immediately.</summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        OnCoinsChanged?.Invoke(coins);
        Save();
    }

    /// <summary>Tries to spend coins. Returns true if successful.</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (coins < amount) return false;
        coins -= amount;
        OnCoinsChanged?.Invoke(coins);
        Save();
        return true;
    }

    /// <summary>Sets the coin balance directly (clamped to >= 0).</summary>
    public void SetCoins(int amount)
    {
        coins = Mathf.Max(0, amount);
        OnCoinsChanged?.Invoke(coins);
        Save();
    }

    /// <summary>Checks if the wallet has enough coins.</summary>
    public bool HasCoins(int amount) => coins >= amount;
    #endregion

    #region Persistence
    private void Save()
    {
        PlayerPrefs.SetInt(PREF_COINS, Mathf.Max(0, coins));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        coins = PlayerPrefs.GetInt(PREF_COINS, startingCoins);
        OnCoinsChanged?.Invoke(coins);
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Add 100 Coins")]
    private void Debug_AddCoins()
    {
        AddCoins(100);
        Debug.Log($"[WalletService] Coins: {coins}");
    }
#endif
}