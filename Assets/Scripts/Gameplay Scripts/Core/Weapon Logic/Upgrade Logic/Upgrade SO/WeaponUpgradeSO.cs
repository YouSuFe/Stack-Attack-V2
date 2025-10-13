using UnityEngine;

[CreateAssetMenu(fileName = "WeaponUpgrade", menuName = "Upgrades/Weapon Upgrade")]
public class WeaponUpgradeSO : ScriptableObject
{
    [Header("Scope")]
    [Tooltip("Defines whether this upgrade affects all weapons or only one specific weapon.")]
    [SerializeField] private WeaponUpgradeScope scope = WeaponUpgradeScope.SpecificWeapon;

    [Tooltip("Used only if Scope is 'SpecificWeapon'. Select which weapon this upgrade targets.")]
    [SerializeField] private WeaponType targetWeaponType = WeaponType.Basic;

    [Header("Effect")]
    [Tooltip("Type of upgrade: FireRatePercent, ProjectileAmount, or Piercing.")]
    [SerializeField] private WeaponUpgradeType upgradeType = WeaponUpgradeType.ProjectileAmount;

    [Tooltip("Rarity of upgrade: FireRatePercent, ProjectileAmount, or Piercing.")]
    [SerializeField] private WeaponUpgradeRarity rarity = WeaponUpgradeRarity.Common;

    [Tooltip("Percentage increase for FireRatePercent upgrades (e.g., 0.25 = +25% faster fire rate).")]
    [SerializeField] private float fireRatePercentValue = 0.25f;

    [Tooltip("Flat amount added for ProjectileAmount or Piercing upgrades (e.g., +1 projectile, +2 pierce).")]
    [SerializeField] private int flatValue = 1;

    public WeaponUpgradeScope Scope => scope;
    public WeaponType TargetWeaponType => targetWeaponType;
    public WeaponUpgradeType UpgradeType => upgradeType;
    public WeaponUpgradeRarity Rarity => rarity;

    public float FireRatePercentValue => fireRatePercentValue;
    public int FlatValue => flatValue;
}
