using UnityEngine;
/// <summary>
/// Attach to a coin prefab (2D).
/// Requirements:
/// - Coin must have a Collider2D with "Is Trigger" checked.
/// - Player must have a Collider2D (and usually a Rigidbody2D for physics events).
/// On trigger with the player, awards coins (integer), plays optional feedback, and DESTROYS the coin.
/// </summary>
[DisallowMultipleComponent]
public class CoinCollectible2D : MonoBehaviour
{
    #region Inspector
    [Header("Collector")]
    [Tooltip("Tag used to detect the player object in 2D.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Optional Feedback")]
    [Tooltip("Optional pickup sound (AudioSource.PlayOneShot or preconfigured AudioSource).")]
    [SerializeField] private AudioSource pickupSfx;

    [Tooltip("Optional particle to play on pickup (will be played before destroy if assigned).")]
    [SerializeField] private ParticleSystem pickupVfx;

    [Tooltip("Time to delay destruction after playing VFX/SFX (seconds). 0 = immediate destroy.")]
    [SerializeField, Min(0f)] private float destroyDelay = 0f;
    #endregion

    #region Private
    private bool collected;
    #endregion

    #region Unity (2D Trigger)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (other == null) return;
        if (!other.CompareTag(playerTag)) return;

        Collect();
    }
    #endregion

    #region Internal
    private void Collect()
    {
        collected = true;

        if (CoinSystem.Instance != null)
        {
            CoinSystem.Instance.AwardSinglePickup();
        }
        else
        {
            Debug.LogWarning("[CoinCollectible2D] No CoinSystem found in scene. Coin not counted.");
        }

        if (CoinPickupUIFX.Instance != null)
        {
            // Second arg is optional visual override per-pickup: 1..5
            CoinPickupUIFX.Instance.PlayFromWorld(transform.position);
        }

        // Fire optional feedback
        if (pickupSfx != null) pickupSfx.Play();
        if (pickupVfx != null) pickupVfx.Play();

        if (destroyDelay <= 0f)
        {
            Destroy(gameObject);
        }
        else
        {
            // Destroy after short delay to let SFX/VFX trigger; coin object itself can still be removed visually
            Destroy(gameObject, destroyDelay);
        }
    }
    #endregion

    private void OnDisable()
    {
        // Safety for reused prefab instances in editor; not pooling, but avoids stuck state if re-enabled
        collected = false;
    }
}
