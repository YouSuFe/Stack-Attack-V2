using System.Collections.Generic;
using UnityEngine;

public class WeaponDriver : MonoBehaviour
{
    [Header("Control")]
    [SerializeField] private bool canAttack = true;

    [Header("Equipped Weapons (components implementing IWeapon)")]
    [SerializeField] private List<MonoBehaviour> equippedWeaponComponents;

    private readonly List<IWeapon> equippedWeapons = new List<IWeapon>();

    private void Awake()
    {
        equippedWeapons.Clear();
        foreach (var mb in equippedWeaponComponents)
        {
            if (mb is IWeapon w) equippedWeapons.Add(w);
            else Debug.LogWarning($"WeaponDriver: {mb.name} does not implement IWeapon.");
        }
        SetCanAttack(canAttack);
    }

    public void SetCanAttack(bool value)
    {
        canAttack = value;
        foreach (var weapon in equippedWeapons)
            if (weapon is BaseWeapon bw) bw.SetCanAttack(value);
    }

    public void RegisterWeapon(IWeapon weapon, WeaponDefinitionSO definition)
    {
        if (weapon == null || definition == null) return;
        if (!equippedWeapons.Contains(weapon)) equippedWeapons.Add(weapon);

        weapon.Initialize(definition, gameObject);
        if (weapon is BaseWeapon bw) bw.SetCanAttack(canAttack);
    }

    public void ApplyUpgrade(WeaponUpgradeSO upgrade)
    {
        if (upgrade == null) return;

        foreach (var weapon in equippedWeapons)
            weapon.ApplyUpgrade(upgrade);
    }

    public void FireOnceAll()
    {
        if (!canAttack) return;
        foreach (var weapon in equippedWeapons) weapon.FireOnce();
    }

    public void StartAutoFireAll()
    {
        if (!canAttack) return;
        foreach (var weapon in equippedWeapons) weapon.StartAutoFire();
    }

    public void StopAutoFireAll()
    {
        foreach (var weapon in equippedWeapons) weapon.StopAutoFire();
    }
}
