using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rolls N cards, deciding the type of EACH card independently:
/// - Per-card 50% chance to be New Weapon (if any unowned exist).
/// - Otherwise Upgrade (weighted by rarity × scope).
/// - First roll guarantee: at least one New Weapon if any unowned exist.
/// Distinct within a roll (no duplicate weapon cards, no duplicate upgrade SOs).
/// </summary>
public class PowerupManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponDriver weaponDriver;
    [SerializeField] private WeaponCatalog weaponCatalog;
    [Tooltip("All upgrade assets that can appear as cards.")]
    [SerializeField] private List<WeaponUpgradeSO> upgradePool = new List<WeaponUpgradeSO>();

    [Header("Offer Settings")]
    [Tooltip("Number of cards per roll (usually 3).")]
    [SerializeField] private int offersPerRoll = 3;

    [Tooltip("Guarantee at least one NEW WEAPON on the FIRST roll if any unowned exist.")]
    [SerializeField] private bool guaranteeNewWeaponOnFirstRoll = true;

    [Tooltip("Per-card chance (0..1) to make that card a NEW WEAPON if any unowned exist.")]
    [Range(0f, 1f)]
    [SerializeField] private float perCardNewWeaponChance = 0.50f;

    [Header("Rarity Weights (relative)")]
    [SerializeField] private int commonWeight = 60;
    [SerializeField] private int rareWeight = 30;
    [SerializeField] private int epicWeight = 9;
    [SerializeField] private int legendaryWeight = 1;

    [Header("Scope Weights (Upgrade offers only)")]
    [Tooltip("Weight multiplier for Specific-weapon upgrades.")]
    [SerializeField] private int specificUpgradeWeight = 80;
    [Tooltip("Weight multiplier for Global upgrades (AllWeapons).")]
    [SerializeField] private int globalUpgradeWeight = 20;

    private bool firstRollDone;
    private readonly List<PowerupOffer> lastRolledOffers = new List<PowerupOffer>(3);

    // =======================================================================
    // PUBLIC API
    // =======================================================================

    public List<PowerupOffer> RollOffers()
    {
        lastRolledOffers.Clear();

        // Build candidate pools
        List<WeaponType> missingWeapons = GetMissingWeaponTypes();             // mutable; we’ll remove as we use
        List<WeaponUpgradeSO> validUpgrades = GetValidUpgradesForCurrentLoadout(); // mutable; we’ll remove as we use

        // Working bags to enforce distinct picks within this roll
        List<WeaponType> weaponBag = new List<WeaponType>(missingWeapons);
        List<WeaponUpgradeSO> upgradeBag = new List<WeaponUpgradeSO>(validUpgrades);

        bool showedWeaponThisRoll = false;

        // Per-card selection
        for (int slot = 0; slot < offersPerRoll; slot++)
        {
            bool canShowWeapon = weaponBag.Count > 0;
            bool makeWeaponCard = false;

            if (canShowWeapon)
                makeWeaponCard = Random.value < perCardNewWeaponChance;

            if (makeWeaponCard)
            {
                // New Weapon card (distinct weapon within the roll)
                WeaponType type = PickAndRemoveRandomWeaponType(weaponBag);
                lastRolledOffers.Add(BuildNewWeaponOffer(type));
                showedWeaponThisRoll = true;
            }
            else
            {
                // Upgrade card (weighted, distinct upgrade within the roll)
                WeaponUpgradeSO up = PickOneWeightedUpgrade(upgradeBag);
                if (up != null)
                {
                    lastRolledOffers.Add(BuildUpgradeOffer(up));
                    upgradeBag.Remove(up);
                }
                else if (canShowWeapon)
                {
                    // Fallback: if no upgrades are available, use a weapon instead
                    WeaponType type = PickAndRemoveRandomWeaponType(weaponBag);
                    lastRolledOffers.Add(BuildNewWeaponOffer(type));
                    showedWeaponThisRoll = true;
                }
                else
                {
                    // Nothing left (very edge). Leave slot empty or duplicate last; we’ll leave empty by not adding.
                }
            }
        }

        // First roll guarantee: ensure at least one new weapon if any unowned exist
        if (!firstRollDone && guaranteeNewWeaponOnFirstRoll && GetMissingWeaponTypes().Count > 0 && !showedWeaponThisRoll)
        {
            // Replace a random upgrade card with a new weapon (if we can)
            int indexToReplace = FindAnyUpgradeIndex(lastRolledOffers);
            if (indexToReplace >= 0)
            {
                // Use the (possibly reduced) weapon bag; if empty, re-create from remaining missing
                if (weaponBag.Count == 0)
                {
                    // Rebuild from current missing (don’t re-offer already equipped)
                    List<WeaponType> freshMissing = GetMissingWeaponTypes();
                    // Prevent re-offering a weapon already in this roll
                    for (int i = 0; i < lastRolledOffers.Count; i++)
                        if (lastRolledOffers[i].offerType == PowerupOfferType.NewWeapon)
                            freshMissing.Remove(lastRolledOffers[i].weaponType);

                    weaponBag = freshMissing;
                }

                if (weaponBag.Count > 0)
                {
                    WeaponType type = PickAndRemoveRandomWeaponType(weaponBag);
                    lastRolledOffers[indexToReplace] = BuildNewWeaponOffer(type);
                    showedWeaponThisRoll = true;
                }
            }
        }

        firstRollDone = true;
        return new List<PowerupOffer>(lastRolledOffers);
    }

    public void ApplyOffer(PowerupOffer offer)
    {
        if (offer == null) return;

        if (offer.offerType == PowerupOfferType.NewWeapon)
        {
            weaponDriver.Equip(offer.weaponType);
        }
        else
        {
            weaponDriver.ApplyUpgrade(offer.upgrade);
        }
    }

    // =======================================================================
    // INTERNAL: PICK HELPERS
    // =======================================================================

    private WeaponUpgradeSO PickOneWeightedUpgrade(List<WeaponUpgradeSO> bag)
    {
        if (bag == null || bag.Count == 0)
            return null;

        int total = 0;
        for (int i = 0; i < bag.Count; i++)
            total += GetTotalWeight(bag[i]);

        if (total <= 0)
        {
            int idx = Random.Range(0, bag.Count);
            return bag[idx];
        }

        int roll = Random.Range(0, total);
        int running = 0;

        for (int i = 0; i < bag.Count; i++)
        {
            running += GetTotalWeight(bag[i]);
            if (roll < running)
                return bag[i];
        }

        return bag[bag.Count - 1]; // safety
    }

    private int GetTotalWeight(WeaponUpgradeSO up)
    {
        int rarityW = GetRarityWeight(up.Rarity);
        int scopeW = up.Scope == WeaponUpgradeScope.AllWeapons ? globalUpgradeWeight : specificUpgradeWeight;
        int total = rarityW * Mathf.Max(0, scopeW);
        return Mathf.Max(0, total);
    }

    private int GetRarityWeight(WeaponUpgradeRarity rarity)
    {
        switch (rarity)
        {
            case WeaponUpgradeRarity.Common: return Mathf.Max(0, commonWeight);
            case WeaponUpgradeRarity.Rare: return Mathf.Max(0, rareWeight);
            case WeaponUpgradeRarity.Epic: return Mathf.Max(0, epicWeight);
            case WeaponUpgradeRarity.Legendary: return Mathf.Max(0, legendaryWeight);
            default: return 0;
        }
    }

    private int FindAnyUpgradeIndex(List<PowerupOffer> offers)
    {
        for (int i = 0; i < offers.Count; i++)
            if (offers[i].offerType == PowerupOfferType.Upgrade)
                return i;
        return -1;
    }

    // =======================================================================
    // INTERNAL: OFFER BUILDERS
    // =======================================================================

    private PowerupOffer BuildNewWeaponOffer(WeaponType type)
    {
        PowerupOffer offer = new PowerupOffer();
        offer.offerType = PowerupOfferType.NewWeapon;
        offer.weaponType = type;
        offer.upgrade = null;

        // Card fields
        offer.rarityText = "New Weapon";
        offer.weaponNameText = type.ToString();      // "Basic", "Missile", "Kunai"
        offer.effectNameText = string.Empty;         // no stat row for new weapon card
        offer.effectValueText = string.Empty;

        return offer;
    }

    private PowerupOffer BuildUpgradeOffer(WeaponUpgradeSO upgrade)
    {
        PowerupOffer offer = new PowerupOffer();
        offer.offerType = PowerupOfferType.Upgrade;
        offer.weaponType = upgrade.Scope == WeaponUpgradeScope.SpecificWeapon ? upgrade.TargetWeaponType : WeaponType.Basic; // UI hint only
        offer.upgrade = upgrade;

        // --- Card fields ---
        // Rarity text
        offer.rarityText = upgrade.Rarity.ToString(); // "Common"/"Rare"/"Epic"/"Legendary"

        // Weapon (scope) text
        offer.weaponNameText = (upgrade.Scope == WeaponUpgradeScope.AllWeapons)
            ? "All Weapons"
            : upgrade.TargetWeaponType.ToString();

        // Effect name + value
        switch (upgrade.UpgradeType)
        {
            case WeaponUpgradeType.ProjectileAmount:
                offer.effectNameText = "Projectiles";
                offer.effectValueText = string.Format("+{0}", upgrade.FlatValue); // e.g., +1 / +2 / +4
                break;

            case WeaponUpgradeType.Piercing:
                offer.effectNameText = "Piercing";
                offer.effectValueText = string.Format("+{0}", upgrade.FlatValue); // e.g., +2 / +3
                break;

            case WeaponUpgradeType.FireRate:
                offer.effectNameText = "Fire Rate";
                // If your enum is FireRatePercent, display as percent. If it's "FireRate", still percent based on your SO
                offer.effectValueText = string.Format("+{0:P0}", upgrade.FireRatePercentValue); // e.g., +15%
                break;
        }

        return offer;
    }

    // =======================================================================
    // INPUT POOLS
    // =======================================================================

    private List<WeaponType> GetMissingWeaponTypes()
    {
        List<WeaponType> missing = new List<WeaponType>();
        if (weaponCatalog == null) return missing;

        foreach (WeaponType type in System.Enum.GetValues(typeof(WeaponType)))
        {
            if (!weaponCatalog.Contains(type)) continue;
            if (!weaponDriver.IsEquipped(type))
                missing.Add(type);
        }

        return missing;
    }

    private List<WeaponUpgradeSO> GetValidUpgradesForCurrentLoadout()
    {
        List<WeaponUpgradeSO> valids = new List<WeaponUpgradeSO>();

        for (int i = 0; i < upgradePool.Count; i++)
        {
            WeaponUpgradeSO up = upgradePool[i];
            if (up == null) continue;

            bool ownershipOk =
                up.Scope == WeaponUpgradeScope.AllWeapons ||
                weaponDriver.IsEquipped(up.TargetWeaponType);

            if (!ownershipOk) continue;

            valids.Add(up);
        }

        return valids;
    }

    private WeaponType PickAndRemoveRandomWeaponType(List<WeaponType> list)
    {
        int index = Random.Range(0, list.Count);
        WeaponType t = list[index];
        list.RemoveAt(index);
        return t;
    }
}
