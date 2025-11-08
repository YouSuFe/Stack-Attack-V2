using UnityEngine;

public interface IDamageDealer
{
    int DamageAmount { get; }
    GameObject Owner { get; }
}
