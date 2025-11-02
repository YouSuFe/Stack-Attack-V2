using UnityEngine;

/// <summary>
/// 2D Trap that damages ONLY the Player when they enter its trigger.
/// - Requires a Collider2D with Is Trigger = true.
/// - Uses IDamageDealer to report damage amount and source.
/// - PlayerHealth handles invulnerability after damage.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class TrapDamageDealer2D : MonoBehaviour, IDamageDealer
{
    #region Inspector
    [Header("Trap Settings")]
    [Tooltip("Damage dealt to the player on contact.")]
    [SerializeField, Min(1)] private int damageAmount = 1;

    [Header("Player Filter")]
    [Tooltip("Only objects with this tag will be damaged. Set your player tag here.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Optional Feedback")]
    [SerializeField] private ParticleSystem triggerVfx; // optional
    [SerializeField] private AudioSource triggerSfx;     // optional
    #endregion

    #region IDamageDealer
    public int DamageAmount => damageAmount;
    public GameObject Owner => gameObject;
    #endregion

    #region Unity
    private void Reset()
    {
        // Ensure trigger mode for overlap events
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // Damage ONLY the Player (by tag)
        if (!other.CompareTag(playerTag)) return;

        // Check for IDamageable on the Player (PlayerHealth implements it)
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null) return;
        if (!damageable.IsAlive) return;

        // Apply 1 (or configured) damage. PlayerHealth will handle invulnerability window.
        damageable.TakeDamage(damageAmount, gameObject);
        Debug.Log($"[TrapDamageDealer2D] Dealt {damageAmount} damage to {other.name}");

        if (triggerSfx != null) triggerSfx.Play();
        if (triggerVfx != null) triggerVfx.Play();
    }
    #endregion
}
