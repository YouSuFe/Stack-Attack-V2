using UnityEngine;
using TMPro;

/// <summary>
/// HUD coin display. Shows VALUED coins (pickups × level coin value).
/// Uses LevelRewardsProvider to convert raw pickup count on every change.
/// </summary>
public class CoinUIController : MonoBehaviour
{
    #region Inspector
    [Header("References")]
    [SerializeField, Tooltip("Will auto-resolve from singleton if left empty.")]
    private CoinSystem coinSystem;

    [SerializeField, Tooltip("Provides current LevelRewardDefinition.")]
    private LevelRewardsProvider rewardsProvider;

    [SerializeField, Tooltip("TMP text to show the valued coin amount.")]
    private TextMeshProUGUI coinText;
    #endregion

    #region Unity
    private void Awake()
    {
        if (coinSystem == null) coinSystem = CoinSystem.Instance;
        if (coinText == null) Debug.LogError("[CoinUIController] coinText not assigned.");
    }

    private void OnEnable()
    {
        if (rewardsProvider != null)
            rewardsProvider.OnRewardsAvailable += HandleRewardsAvailable;

        if (coinSystem != null)
        {
            coinSystem.OnCoinsChanged += HandleCoinsChanged;
            HandleCoinsChanged(coinSystem.PickupCount); // initial render
        }
        else
        {
            Debug.LogWarning("[CoinUIController] No CoinSystem available.");
        }
    }

    private void OnDisable()
    {
        if (rewardsProvider != null)
            rewardsProvider.OnRewardsAvailable -= HandleRewardsAvailable;

        if (coinSystem != null)
            coinSystem.OnCoinsChanged -= HandleCoinsChanged;
    }
    #endregion

    #region Handlers
    private void HandleRewardsAvailable(LevelRewardDefinition _)
    {
        // Recompute with current rules when rewards resolve.
        if (coinSystem != null) HandleCoinsChanged(coinSystem.PickupCount);
    }

    private void HandleCoinsChanged(int pickupCount)
    {
        if (coinText == null) return;

        int display = rewardsProvider != null
            ? rewardsProvider.ToMetaCoins(pickupCount)
            : pickupCount;

        coinText.text = display.ToString();
    }
    #endregion
}

