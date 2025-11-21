using UnityEngine;

/// <summary>
/// Generic 2D trigger collectible:
/// - Requires the Player to have a Collider2D (and usually Rigidbody2D).
/// - This object should have a Collider2D set to "Is Trigger".
/// - Handles common pickup feedback (sound + VFX).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class CollectibleBase : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("If true, only colliders with this tag can collect it.")]
    [SerializeField] private bool requirePlayerTag = true;

    [SerializeField, Tooltip("Tag allowed to collect this item. Used only if requirePlayerTag = true.")]
    private string playerTag = "Player";

    [Tooltip("Auto-destroy on pickup.")]
    [SerializeField] private bool destroyOnPickup = true;

    [Header("Feedback")]
    [Tooltip("Sound played when picked up (2D).")]
    [SerializeField] private SoundData pickupSound;

    protected virtual void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (requirePlayerTag && !other.CompareTag(playerTag))
            return;

        GameObject player = other.gameObject;

        if (OnCollected(player))
        {
            PlayPickupFeedback();

            if (destroyOnPickup)
                Destroy(gameObject);
        }
    }

    /// <summary>
    /// Perform the collectible effect. Return true if consumed.
    /// </summary>
    protected abstract bool OnCollected(GameObject player);

    /// <summary>
    /// Common feedback: sound only. VFX handled in child classes.
    /// </summary>
    protected virtual void PlayPickupFeedback()
    {
        if (pickupSound != null)
            SoundUtils.Play2D(pickupSound);
    }
}
