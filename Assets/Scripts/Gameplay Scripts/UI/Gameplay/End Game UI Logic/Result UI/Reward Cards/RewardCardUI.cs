using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple reward card with icon + amount label. Style it in the prefab.
/// </summary>
[DisallowMultipleComponent]
public class RewardCardUI : MonoBehaviour
{
    #region Private Fields
    [SerializeField, Tooltip("Icon renderer for the reward.")]
    private Image iconImage;

    [SerializeField, Tooltip("Amount text (TMP).")]
    private TMP_Text amountText;
    #endregion

    #region Public Methods
    public void Set(Sprite icon, int amount)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (amountText != null)
        {
            amountText.text = $"x{amount}";
        }
    }
    #endregion
}

