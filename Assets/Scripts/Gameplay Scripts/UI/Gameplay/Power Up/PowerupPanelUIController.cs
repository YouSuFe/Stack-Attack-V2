using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PowerupPanelUIController : MonoBehaviour
{
    #region Inspector Fields

    [Header("Logic References")]
    [SerializeField] private PowerupManager powerupManager;
    [SerializeField] private WeaponIconLibrary weaponIconLibrary;

    [Header("Card Slots")]
    [SerializeField] private PowerupCardView cardLeft;
    [SerializeField] private PowerupCardView cardCenter;
    [SerializeField] private PowerupCardView cardRight;

    [Header("Buttons")]
    [Tooltip("Rerolls the current set of offers without applying any.")]
    [SerializeField] private Button buttonReroll;
    [Tooltip("Applies all shown offers in one click.")]
    [SerializeField] private Button buttonClaimAll;

    [Header("Frame Colors")]
    [SerializeField] private Color frameCommon = new(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color frameRare = new(0.45f, 0.65f, 1.00f);
    [SerializeField] private Color frameEpic = new(0.70f, 0.40f, 1.00f);
    [SerializeField] private Color frameLegendary = new(1.00f, 0.80f, 0.10f);
    [SerializeField] private Color frameNewWeapon = new(0.90f, 0.90f, 0.90f);

    [Header("Text Colors (high contrast)")]
    [SerializeField] private Color textCommon = new(0.15f, 0.15f, 0.15f);
    [SerializeField] private Color textRare = new(0.4f, 0.65f, 1f);
    [SerializeField] private Color textEpic = new(0.75f, 0.5f, 1f);
    [SerializeField] private Color textLegendary = new(1f, 0.85f, 0.25f);
    [SerializeField] private Color textNewWeapon = new(0.9f, 0.9f, 0.9f);

    #endregion

    #region Private Fields

    private readonly List<PowerupOffer> currentRolledOffers = new();
    private bool isProcessing;

    #endregion

    #region Events

    // Fired when the player applies/accepts a card (one level-up resolved).
    public event Action OnCardApplied;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (buttonReroll != null) buttonReroll.onClick.AddListener(OnRerollClicked);
        if (buttonClaimAll != null) buttonClaimAll.onClick.AddListener(OnClaimAllClicked);
    }

    private void OnDestroy()
    {
        if (buttonReroll != null) buttonReroll.onClick.RemoveListener(OnRerollClicked);
        if (buttonClaimAll != null) buttonClaimAll.onClick.RemoveListener(OnClaimAllClicked);
    }

    #endregion

    #region Public API

    public void ShowAndRoll()
    {
        gameObject.SetActive(true);
        RollAndBindCards();
        SetAllCardInteractivity(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void OnApplyCard(PowerupOffer selectedOffer)
    {
        if (selectedOffer == null) return;
        if (powerupManager == null)
        {
            Debug.LogError("[PowerupPanelUIController] PowerupManager reference not set.");
            return;
        }

        powerupManager.ApplyOffer(selectedOffer);
        OnCardApplied?.Invoke();
    }

    #endregion

    #region Internal Helpers

    private void RollAndBindCards()
    {
        if (powerupManager == null)
        {
            Debug.LogError("[PowerupPanelUIController] PowerupManager is not assigned; cannot roll cards.");
            return;
        }

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

            cardView.Bind(this, offer, icon, frameColor, textColor);
        }
        else
        {
            cardView.Clear();
        }
    }

    private Sprite GetIconForOffer(PowerupOffer offer)
    {
        if (weaponIconLibrary == null) return null;
        if (offer == null) return weaponIconLibrary.GetUpgradeIcon();

        if (offer.offerType == PowerupOfferType.NewWeapon)
            return weaponIconLibrary.GetWeaponIcon(offer.weaponType);

        if (offer.upgrade != null && offer.upgrade.Scope == WeaponUpgradeScope.SpecificWeapon)
            return weaponIconLibrary.GetWeaponIcon(offer.upgrade.TargetWeaponType);

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

    private void SetAllCardInteractivity(bool interactable)
    {
        if (cardLeft != null) cardLeft.SetInteractable(interactable);
        if (cardCenter != null) cardCenter.SetInteractable(interactable);
        if (cardRight != null) cardRight.SetInteractable(interactable);

        if (buttonReroll != null) buttonReroll.interactable = interactable;
        if (buttonClaimAll != null) buttonClaimAll.interactable = interactable;
    }

    #endregion

    #region Button Handlers

    private void OnRerollClicked()
    {
        if (isProcessing) return;

        // Optional: play sound or animation here.
        RollAndBindCards();
    }

    private void OnClaimAllClicked()
    {
        if (isProcessing) return;
        if (powerupManager == null)
        {
            Debug.LogError("[PowerupPanelUIController] PowerupManager reference not set.");
            return;
        }

        StartCoroutine(ClaimAllCoroutine());
    }

    private IEnumerator ClaimAllCoroutine()
    {
        isProcessing = true;
        SetAllCardInteractivity(false);

        var offersSnapshot = new List<PowerupOffer>(currentRolledOffers);

        foreach (var offer in offersSnapshot)
        {
            if (offer == null) continue;
            powerupManager.ApplyOffer(offer);

            // Optional: yield between applications for smooth visual feedback
            yield return null;
        }

        isProcessing = false;
        OnCardApplied?.Invoke();
    }

    #endregion
}
