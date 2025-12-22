using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponIconLibrary", menuName = "Game/Weapon Icon Library")]
public class WeaponIconLibrary : ScriptableObject
{
    [Serializable]
    private class WeaponIconEntry
    {
        public WeaponType weaponType;
        public Sprite sprite;
    }

    [Header("Icons per Weapon Type")]
    [SerializeField] private List<WeaponIconEntry> weaponIcons = new List<WeaponIconEntry>();

    [Header("Fallbacks")]
    [SerializeField] private Sprite defaultWeaponIcon;
    [SerializeField] private Sprite defaultUpgradeIcon;
    [SerializeField] private Sprite defaultAllWeaponsUpgradeIcon;

    public Sprite GetWeaponIcon(WeaponType type)
    {
        for (int i = 0; i < weaponIcons.Count; i++)
            if (weaponIcons[i].weaponType == type && weaponIcons[i].sprite != null)
                return weaponIcons[i].sprite;

        return defaultWeaponIcon;
    }

    public Sprite GetUpgradeIcon()
    {
        return defaultUpgradeIcon != null ? defaultUpgradeIcon : defaultWeaponIcon;
    }

    public Sprite GetAllWeaponsUpgradeIcon()
    {
        return defaultAllWeaponsUpgradeIcon != null ? defaultAllWeaponsUpgradeIcon : defaultUpgradeIcon;
    }
}

