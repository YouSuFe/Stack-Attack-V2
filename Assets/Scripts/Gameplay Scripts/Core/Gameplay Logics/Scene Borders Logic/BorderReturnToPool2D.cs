using UnityEngine;

/// <summary>
/// 2D trigger volume that returns pooled objects (e.g., projectiles) to their pools
/// when they enter the border area. If an entering object doesn't implement
/// IPoolDespawnable, it is ignored.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class BorderReturnToPool2D : MonoBehaviour
{
    #region Unity Lifecycle
    private void Awake()
    {
        // Ensure this works as a trigger volume
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only handle pooled objects that explicitly declare pool-despanability
        if (other.TryGetComponent<IPoolDespawnable>(out var poolable))
        {
            poolable.DespawnToPool();
        }
    }
    #endregion
}
