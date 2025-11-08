using UnityEngine;

public interface IDamageable
{
    void TakeDamage(int damageAmount, GameObject damageSource);
    bool IsAlive { get; }
}
