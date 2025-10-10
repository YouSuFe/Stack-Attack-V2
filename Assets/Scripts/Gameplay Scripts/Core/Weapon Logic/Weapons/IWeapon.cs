using UnityEngine;

public interface IWeapon
{
    void Initialize(WeaponDefinitionSO weaponDefinition, GameObject owner);
    void ApplyUpgrade(WeaponUpgradeSO upgrade);

    void FireOnce();
    void StartAutoFire();
    void StopAutoFire();

    bool CanAutoFire { get; }
    bool IsReadyToFire { get; }

    WeaponType WeaponType { get; }
}
