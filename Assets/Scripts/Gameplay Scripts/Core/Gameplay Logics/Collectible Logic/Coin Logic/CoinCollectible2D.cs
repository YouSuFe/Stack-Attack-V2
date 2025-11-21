using UnityEngine;

/// <summary>
/// Coin collectible using the CollectibleBase system.
/// Awards coins, triggers UI FX, and uses base sound & destroy logic.
/// </summary>
public class CoinCollectible2D : CollectibleBase
{
    [Header("Coin Settings")]
    [Tooltip("Number of coins awarded when collected.")]
    [SerializeField] private int coinAmount = 1;

    protected override bool OnCollected(GameObject player)
    {
        // Award coin
        if (CoinSystem.Instance != null)
        {
            CoinSystem.Instance.AddCoins(coinAmount);
        }
        else
        {
            Debug.LogWarning("[CoinCollectible2D] No CoinSystem found in scene.");
        }

        // Play UI FX
        if (CoinPickupUIFX.Instance != null)
        {
            CoinPickupUIFX.Instance.PlayFromWorld(transform.position);
        }

        // Base handles sound + destroy
        return true;
    }
}
