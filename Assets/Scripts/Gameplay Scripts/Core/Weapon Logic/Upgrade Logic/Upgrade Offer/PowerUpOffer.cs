public enum PowerupOfferType
{
    NewWeapon,
    Upgrade
}

[System.Serializable]
public class PowerupOffer
{
    public PowerupOfferType offerType;
    public WeaponType weaponType;            // used for NewWeapon offers
    public WeaponUpgradeSO upgrade;          // used for Upgrade offers

    // Card-facing fields (fill these so UI can be dumb)
    public string rarityText;               // "Common", "Rare", "Epic", "Legendary", or "New Weapon"
    public string weaponNameText;           // "Basic", "Missile", "Kunai", or "All Weapons"
    public string effectNameText;           // "Projectiles", "Fire Rate", "Piercing" (blank for New Weapon)
    public string effectValueText;          // "+1", "+%15", etc (blank for New Weapon)

}