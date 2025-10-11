using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-stop controller for all weapons on the player.
/// - Equips weapons at runtime via a WeaponCatalog (enum -> SO)
/// - Injects scene refs from WeaponMounts through Init(...)
/// - Exposes a small API for input & gating firing
/// - Routes upgrades to all or specific weapons
/// </summary>
public class WeaponDriver : MonoBehaviour
{
    [Header("Scene Mounts & Catalog")]
    [SerializeField] private WeaponMounts weaponMounts;   // Assign on Player
    [SerializeField] private WeaponCatalog weaponCatalog; // Assign catalog asset

    [Header("Global Gate")]
    [SerializeField] private bool canAttack = true;       // Treasure rooms, cutscenes, etc.

    // Runtime equipped weapons (by type and also in a stable list for iteration order)
    private readonly Dictionary<WeaponType, IWeapon> equippedByType = new();
    private readonly List<IWeapon> equippedInOrder = new();

    private void Awake()
    {
        if (weaponMounts == null)
            Debug.LogWarning("WeaponDriver: 'weaponMounts' not assigned on the Player.");

        if (weaponCatalog == null)
            Debug.LogWarning("WeaponDriver: 'weaponCatalog' not assigned.");
    }

    // ---------------------------------------------------------------------
    // EQUIP / UNEQUIP
    // ---------------------------------------------------------------------

    /// <summary>
    /// Ensure a weapon of the given type is equipped.
    /// Looks up its definition in the catalog and equips it if missing.
    /// Returns the IWeapon instance (existing or newly equipped), or null if not found.
    /// </summary>
    public IWeapon Equip(WeaponType type)
    {
        // Already equipped? Return it.
        if (equippedByType.TryGetValue(type, out IWeapon existingWeapon))
            return existingWeapon;

        // Find data in the catalog
        WeaponDefinitionSO definition = weaponCatalog != null ? weaponCatalog.Get(type) : null;
        if (definition == null)
        {
            Debug.LogWarning($"WeaponDriver.Equip: No definition in catalog for {type}");
            return null;
        }

        // Create and initialize the correct component for this type
        IWeapon newWeapon = CreateAndInitComponentFor(type);
        if (newWeapon == null)
        {
            Debug.LogError($"WeaponDriver.Equip: No component mapping for {type}");
            return null;
        }

        // Initialize runtime with data + owner
        newWeapon.Initialize(definition, gameObject);

        // Respect current global gate
        if (newWeapon is BaseWeapon baseWeapon)
            baseWeapon.SetCanAttack(canAttack);

        equippedByType[type] = newWeapon;
        equippedInOrder.Add(newWeapon);

        return newWeapon;
    }

    /// <summary>
    /// Equip directly from a definition (bypasses catalog lookup).
    /// Useful for tests; in production prefer Equip(WeaponType).
    /// </summary>
    public IWeapon Equip(WeaponDefinitionSO definition)
    {
        if (definition == null) return null;
        return Equip(definition.WeaponType);
    }

    /// <summary>
    /// Unequip & destroy the given weapon type (if equipped).
    /// </summary>
    public void Unequip(WeaponType type)
    {
        if (!equippedByType.TryGetValue(type, out IWeapon weaponToRemove))
            return;

        weaponToRemove.StopAutoFire();
        equippedByType.Remove(type);
        equippedInOrder.Remove(weaponToRemove);

        if (weaponToRemove is MonoBehaviour weaponComponent)
            Destroy(weaponComponent);
    }

    /// <summary>
    /// Returns true if a weapon of this type is currently equipped.
    /// </summary>
    public bool IsEquipped(WeaponType type) => equippedByType.ContainsKey(type);

    // ---------------------------------------------------------------------
    // FIRING CONTROL
    // ---------------------------------------------------------------------

    /// <summary>
    /// Prevent or allow any firing. Disabling also stops auto-fire immediately.
    /// </summary>
    public void SetCanAttack(bool allowAttack)
    {
        canAttack = allowAttack;

        // Propagate gate to all base weapons
        foreach (IWeapon weapon in equippedInOrder)
        {
            if (weapon is BaseWeapon baseWeapon)
                baseWeapon.SetCanAttack(canAttack);
        }

        if (!canAttack)
            StopAutoFireAll();
    }

    /// <summary>
    /// Single-shot tap for all equipped weapons (respects readiness and canAttack).
    /// </summary>
    public void FireOnceAll()
    {
        if (!canAttack) return;

        // Each weapon enforces its own cooldown/readiness
        for (int i = 0; i < equippedInOrder.Count; i++)
            equippedInOrder[i].FireOnce();
    }

    /// <summary>
    /// Begin auto-fire on all equipped weapons that support it.
    /// Typical usage: on input press/hold begin.
    /// </summary>
    public void StartAutoFireAll()
    {
        if (!canAttack) return;

        for (int i = 0; i < equippedInOrder.Count; i++)
            equippedInOrder[i].StartAutoFire();
    }

    /// <summary>
    /// Stop auto-fire on all equipped weapons.
    /// Typical usage: on input release.
    /// </summary>
    public void StopAutoFireAll()
    {
        for (int i = 0; i < equippedInOrder.Count; i++)
            equippedInOrder[i].StopAutoFire();
    }

    /// <summary>
    /// Convenience for input: call with true on press, false on release.
    /// </summary>
    public void OnShootPressed(bool isPressed)
    {
        Debug.Log($"OnShootPressed(isPressed:{isPressed}) called. canAttack={canAttack}, equippedInOrder.Count={equippedInOrder.Count}");

        if (!canAttack)
        {
            Debug.Log("OnShootPressed: Global gate canAttack==false -> ignoring input.");
            return;
        }

        if (isPressed)
        {
            Debug.Log("OnShootPressed: PRESS detected -> FireOnceAll() for immediate feedback, then StartAutoFireAll().");
            FireOnceAll();
            StartAutoFireAll();
        }
        else
        {
            Debug.Log("OnShootPressed: RELEASE detected -> StopAutoFireAll().");
            StopAutoFireAll();
        }
    }

    // ---------------------------------------------------------------------
    // UPGRADES
    // ---------------------------------------------------------------------

    /// <summary>
    /// Apply a WeaponUpgradeSO. Scope decides whether it affects all weapons or a specific weapon type.
    /// </summary>
    public void ApplyUpgrade(WeaponUpgradeSO upgrade)
    {
        if (upgrade == null) return;

        if (upgrade.Scope == WeaponUpgradeScope.AllWeapons)
        {
            for (int i = 0; i < equippedInOrder.Count; i++)
            {
                IWeapon equippedWeapon = equippedInOrder[i];
                equippedWeapon.ApplyUpgrade(upgrade);
            }
        }
        else // SpecificWeapon
        {
            if (equippedByType.TryGetValue(upgrade.TargetWeaponType, out IWeapon targetWeapon))
            {
                targetWeapon.ApplyUpgrade(upgrade);
            }
            // If you want upgrades to apply even before a weapon is equipped,
            // you can add a "pending upgrades cache" keyed by WeaponType and apply them inside Equip(...).
        }
    }

    /// <summary>
    /// Apply a batch of upgrades (e.g., when loading a save or opening a chest).
    /// </summary>
    public void ApplyUpgrades(IEnumerable<WeaponUpgradeSO> upgrades)
    {
        if (upgrades == null) return;

        foreach (WeaponUpgradeSO upgrade in upgrades)
            ApplyUpgrade(upgrade);
    }

    // ---------------------------------------------------------------------
    // INTERNAL FACTORY: create component for type and inject mounts
    // ---------------------------------------------------------------------

    /// <summary>
    /// Adds the correct weapon component at runtime and injects scene refs via Init(...).
    /// If you prefer pre-attached components, swap to GetComponent<...>() and enable/init them.
    /// </summary>
    private IWeapon CreateAndInitComponentFor(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Basic:
                {
                    var basicWeapon = gameObject.AddComponent<BasicWeapon>();
                    basicWeapon.Init(
                        fireOrigin: weaponMounts != null ? weaponMounts.BasicFireOrigin : null,
                        horizontalSpacing: weaponMounts != null ? weaponMounts.BasicHorizontalSpacing : 0.7f,
                        secondRowVerticalOffset: weaponMounts != null ? weaponMounts.BasicRowVerticalOffset : 0.15f
                    );
                    return basicWeapon;
                }

            case WeaponType.Missile:
                {
                    var missileWeapon = gameObject.AddComponent<MissileWeapon>();
                    missileWeapon.Init(
                        fireOrigin: weaponMounts != null ? weaponMounts.MissileFireOrigin : null,
                        leftMuzzle: weaponMounts != null ? weaponMounts.MissileLeftMuzzle : null,
                        rightMuzzle: weaponMounts != null ? weaponMounts.MissileRightMuzzle : null,
                        fallbackSideOffsetX: weaponMounts != null ? weaponMounts.MissileFallbackSideOffsetX : 0.6f
                    );
                    return missileWeapon;
                }

            case WeaponType.Kunai:
                {
                    var kunaiWeapon = gameObject.AddComponent<KunaiWeapon>();
                    kunaiWeapon.Init(
                        fireOrigin: weaponMounts != null ? weaponMounts.KunaiFireOrigin : null,
                        fanStepDegrees: weaponMounts != null ? weaponMounts.KunaiFanStepDegrees : 5f
                    );
                    return kunaiWeapon;
                }

            default:
                return null;
        }
    }
}
