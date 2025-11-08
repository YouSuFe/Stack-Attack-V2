using System;
using UnityEngine;

/// <summary>
/// Global event for: "the PLAYER's weapon hit an enemy".
/// Does not include damage numbers—each successful first contact counts as 1 hit.
/// </summary>
public static class HitEventBus
{
    /// <param name="target">The hit damageable.</param>
    /// <param name="dealerOwner">Owner GameObject of the dealer (the player GO for player weapons).</param>
    public static event Action<IDamageable, GameObject> OnPlayerHit;

    public static void RaisePlayerHit(IDamageable target, GameObject dealerOwner)
    {
        if (dealerOwner == null || target == null) return;
        OnPlayerHit?.Invoke(target, dealerOwner);
    }
}
