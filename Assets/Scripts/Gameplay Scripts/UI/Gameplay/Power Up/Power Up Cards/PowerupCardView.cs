// PowerupCardView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerupCardView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image imageRarityFrame;
    [SerializeField] private Image imageIcon;

    [SerializeField] private TMP_Text textRarity;        // Top line: "Common / Rare / Epic / Legendary" (or "New Weapon")
    [SerializeField] private TMP_Text textWeaponName;    // "Basic / Kunai / Missile / All Weapons"
    [SerializeField] private TMP_Text textEffectName;    // "Projectiles / Fire Rate / Piercing"
    [SerializeField] private TMP_Text textEffectValue;   // "+1 / +%15 / +2"
    [SerializeField] private Button buttonApply;

    private PowerupOffer boundOffer;
    private PowerupPanelUIController owningPanel;

    private void Awake()
    {
        if (buttonApply != null)
        {
            buttonApply.onClick.RemoveAllListeners();
            buttonApply.onClick.AddListener(OnButtonApplyClicked);
        }
    }

    public void Bind(PowerupPanelUIController panel,
                     PowerupOffer offer,
                     Sprite iconSprite,
                     Color frameColor,
                     Color rarityTextColor)
    {
        owningPanel = panel;
        boundOffer = offer;

        if (imageRarityFrame != null) imageRarityFrame.color = frameColor;
        if (imageIcon != null) imageIcon.sprite = iconSprite;

        if (textRarity != null)
        {
            textRarity.text = offer != null ? offer.rarityText : "-";
            textRarity.color = rarityTextColor;
        }

        if (textWeaponName != null)
            textWeaponName.text = offer != null ? offer.weaponNameText : "-";

        // 🟣 Handle upgrade vs new weapon separately
        if (offer != null)
        {
            if (offer.offerType == PowerupOfferType.Upgrade)
            {
                // --- Regular upgrade ---
                if (textEffectName != null) textEffectName.text = offer.effectNameText;
                if (textEffectValue != null) textEffectValue.text = offer.effectValueText;
            }
            else if (offer.offerType == PowerupOfferType.NewWeapon)
            {
                // --- New weapon card ---
                if (textEffectName != null) textEffectName.text = "";
                if (textEffectValue != null) textEffectValue.text = "Unlock";
            }
        }
        else
        {
            if (textEffectName != null) textEffectName.text = "";
            if (textEffectValue != null) textEffectValue.text = "";
        }

        SetInteractable(offer != null);
        gameObject.SetActive(true);
    }

    public void SetInteractable(bool enabled)
    {
        if (buttonApply != null)
            buttonApply.interactable = enabled;
    }

    public void Clear()
    {
        owningPanel = null;
        boundOffer = null;

        if (imageIcon != null) imageIcon.sprite = null;
        if (textRarity != null) textRarity.text = "-";
        if (textWeaponName != null) textWeaponName.text = "-";
        if (textEffectName != null) textEffectName.text = "";
        if (textEffectValue != null) textEffectValue.text = "";

        SetInteractable(false);
        gameObject.SetActive(true);
    }

    private void OnButtonApplyClicked()
    {
        if (owningPanel != null && boundOffer != null)
            owningPanel.OnApplyCard(boundOffer);
    }
}
