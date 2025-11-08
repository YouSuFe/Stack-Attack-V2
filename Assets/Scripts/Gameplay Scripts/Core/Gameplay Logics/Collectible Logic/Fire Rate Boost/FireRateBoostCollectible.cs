// CollectibleBase.cs
using UnityEngine;

public class FireRateBoostCollectible : CollectibleBase
{
    [Header("Effect")]
    [Tooltip("Multiply current fire-rate by this factor, e.g., 5 = five times faster.")]
    [SerializeField] private float fireRateMultiplier = 5f;

    [Tooltip("How long the boost lasts (seconds).")]
    [SerializeField] private float durationSeconds = 5f;

    [Header("Discovery")]
    [Tooltip("If null, we will search on the Player.")]
    [SerializeField] private WeaponDriver weaponDriver;

    protected override bool OnCollected(GameObject player)
    {
        if (weaponDriver == null)
            weaponDriver = player.GetComponent<WeaponDriver>();

        if (weaponDriver == null)
        {
            Debug.LogWarning("FireRateBoostCollectible: Could not find WeaponDriver on player.");
            return false;
        }

        weaponDriver.AddTemporaryGlobalFireRateBoost(fireRateMultiplier, durationSeconds);
        return true;
    }
}
