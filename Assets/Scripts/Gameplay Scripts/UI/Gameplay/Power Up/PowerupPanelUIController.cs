using System.Collections.Generic;
using UnityEngine;

public class PowerupPanelUIController : MonoBehaviour
{
    [Header("Logic References")]
    [SerializeField] private PowerupManager powerupManager;
    [SerializeField] private WeaponIconLibrary weaponIconLibrary;

    [Header("Card Slots")]
    [SerializeField] private PowerupCardView cardLeft;
    [SerializeField] private PowerupCardView cardCenter;
    [SerializeField] private PowerupCardView cardRight;

    [Header("Frame Colors")]
    [SerializeField] private Color frameCommon = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color frameRare = new Color(0.45f, 0.65f, 1.00f);
    [SerializeField] private Color frameEpic = new Color(0.70f, 0.40f, 1.00f);
    [SerializeField] private Color frameLegendary = new Color(1.00f, 0.80f, 0.10f);
    [SerializeField] private Color frameNewWeapon = new Color(0.90f, 0.90f, 0.90f);

    [Header("Text Colors (high contrast)")]
    [SerializeField] private Color textCommon = new Color(0.15f, 0.15f, 0.15f);
    [SerializeField] private Color textRare = new Color(0.4f, 0.65f, 1f);
    [SerializeField] private Color textEpic = new Color(0.75f, 0.5f, 1f);
    [SerializeField] private Color textLegendary = new Color(1f, 0.85f, 0.25f);
    [SerializeField] private Color textNewWeapon = new Color(0.9f, 0.9f, 0.9f);

    private readonly List<PowerupOffer> currentRolledOffers = new();

    // ---------------- PUBLIC API ----------------

    public void ShowAndRoll()
    {
        gameObject.SetActive(true);
        RollAndBindCards();
    }

    public void Hide()
    {
        gameObject.SetActive(false);

        // ToDo: Test Purposes, it will move proper manager
        PauseManager.Instance.ResumeGameplay();
    }

    public void OnApplyCard(PowerupOffer selectedOffer)
    {
        if (selectedOffer == null) return;
        powerupManager.ApplyOffer(selectedOffer);
        Hide();
    }

    // ---------------- INTERNAL ----------------

    private void RollAndBindCards()
    {
        currentRolledOffers.Clear();

        List<PowerupOffer> offers = powerupManager.RollOffers();
        for (int i = 0; i < offers.Count; i++)
            currentRolledOffers.Add(offers[i]);

        BindCardToIndex(cardLeft, 0);
        BindCardToIndex(cardCenter, 1);
        BindCardToIndex(cardRight, 2);
    }

    private void BindCardToIndex(PowerupCardView cardView, int index)
    {
        if (cardView == null) return;

        if (index < currentRolledOffers.Count && currentRolledOffers[index] != null)
        {
            PowerupOffer offer = currentRolledOffers[index];
            Sprite icon = GetIconForOffer(offer);

            (Color frameColor, Color textColor) = GetRarityColorsForOffer(offer);

            cardView.Bind(
                panel: this,
                offer: offer,
                iconSprite: icon,
                frameColor: frameColor,
                rarityTextColor: textColor
            );
        }
        else
        {
            cardView.Clear();
        }
    }

    private Sprite GetIconForOffer(PowerupOffer offer)
    {
        if (weaponIconLibrary == null)
            return null;

        if (offer == null)
            return weaponIconLibrary.GetUpgradeIcon();

        if (offer.offerType == PowerupOfferType.NewWeapon)
            return weaponIconLibrary.GetWeaponIcon(offer.weaponType);

        if (offer.upgrade != null &&
            offer.upgrade.Scope == WeaponUpgradeScope.SpecificWeapon)
        {
            return weaponIconLibrary.GetWeaponIcon(offer.upgrade.TargetWeaponType);
        }

        return weaponIconLibrary.GetUpgradeIcon();
    }

    private (Color frameColor, Color textColor) GetRarityColorsForOffer(PowerupOffer offer)
    {
        if (offer == null)
            return (frameCommon, textCommon);

        if (offer.offerType == PowerupOfferType.NewWeapon)
            return (frameNewWeapon, textNewWeapon);

        if (offer.upgrade == null)
            return (frameCommon, textCommon);

        return offer.upgrade.Rarity switch
        {
            WeaponUpgradeRarity.Rare => (frameRare, textRare),
            WeaponUpgradeRarity.Epic => (frameEpic, textEpic),
            WeaponUpgradeRarity.Legendary => (frameLegendary, textLegendary),
            _ => (frameCommon, textCommon)
        };
    }
}
