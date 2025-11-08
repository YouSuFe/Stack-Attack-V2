using UnityEngine;

/// <summary>
/// Central lookup that maps a WeaponType enum to its WeaponDefinitionSO.
/// Create a single asset in your project and populate it with all weapon definitions.
/// </summary>
[CreateAssetMenu(fileName = "WeaponCatalog", menuName = "Weapons/Weapon Catalog")]
public class WeaponCatalog : ScriptableObject
{
    [SerializeField] private WeaponDefinitionSO[] allWeapons;

    /// <summary>
    /// Returns the definition for the given weapon type, or null if not found in the catalog.
    /// </summary>
    public WeaponDefinitionSO Get(WeaponType type)
    {
        if (allWeapons == null) return null;

        for (int i = 0; i < allWeapons.Length; i++)
        {
            WeaponDefinitionSO definition = allWeapons[i];
            if (definition != null && definition.WeaponType == type)
                return definition;
        }

        return null;
    }

    /// <summary>
    /// True if a definition exists for the given weapon type in this catalog.
    /// </summary>
    public bool Contains(WeaponType type) => Get(type) != null;
}
