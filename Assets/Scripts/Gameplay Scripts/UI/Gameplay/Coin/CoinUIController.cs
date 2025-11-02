using UnityEngine;
using TMPro;

/// <summary>
/// Simple UI controller that updates a TMP text with the integer total coin count.
/// </summary>
public class CoinUIController : MonoBehaviour
{
    #region Inspector
    [Header("References")]
    [SerializeField] private CoinSystem coinSystem;         // Optional: auto-resolves if left empty
    [SerializeField] private TextMeshProUGUI coinText;      // Assign your HUD TMP text here
    #endregion

    private void Awake()
    {
        if (coinSystem == null)
            coinSystem = CoinSystem.Instance;

        if (coinText == null)
            Debug.LogError("[CoinUIController] coinText not assigned.");
    }

    private void OnEnable()
    {
        if (coinSystem != null)
        {
            coinSystem.OnCoinsChanged += HandleCoinsChanged;
            HandleCoinsChanged(coinSystem.TotalCoins);
        }
        else
        {
            Debug.LogWarning("[CoinUIController] No CoinSystem available.");
        }
    }

    private void OnDisable()
    {
        if (coinSystem != null)
            coinSystem.OnCoinsChanged -= HandleCoinsChanged;
    }

    private void HandleCoinsChanged(int total)
    {
        if (coinText == null) return;
        coinText.text = total.ToString(); // integer only
    }
}
