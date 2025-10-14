// CollectibleBase.cs
using UnityEngine;

/// <summary>
/// Generic 2D trigger collectible:
/// - Requires the Player to have a Rigidbody2D (you already do)
/// - This object should have a Collider2D set to "Is Trigger"
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class CollectibleBase : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("Optional: restrict pickup to colliders tagged 'Player'. If false, any collider can pick up.")]
    [SerializeField] private bool requirePlayerTag = true;

    [Tooltip("Auto-destroy on pickup (recommended).")]
    [SerializeField] private bool destroyOnPickup = true;

    [Header("Feedback")]
    [SerializeField] private AudioSource pickupSfx;
    [SerializeField] private GameObject pickupVfx;

    protected virtual void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (requirePlayerTag && !other.CompareTag("Player"))
            return;

        GameObject player = other.gameObject;
        if (OnCollected(player))
        {
            if (pickupSfx) pickupSfx.Play();
            if (pickupVfx) Instantiate(pickupVfx, transform.position, Quaternion.identity);
            if (destroyOnPickup) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Perform the collectible effect. Return true if consumed.
    /// </summary>
    protected abstract bool OnCollected(GameObject player);
}
