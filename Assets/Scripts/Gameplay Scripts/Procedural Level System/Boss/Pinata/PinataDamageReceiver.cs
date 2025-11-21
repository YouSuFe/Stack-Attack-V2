using UnityEngine;

/// <summary>
/// Receives damage only while pinata is enabled. Projectiles will call IDamageable.TakeDamage.
/// Keep ApplyHit for non-projectile callers (cheats, scripts, AOE).
/// </summary>
[DisallowMultipleComponent]
public class PinataDamageReceiver : MonoBehaviour, IDamageable
{
    #region Private Fields
    [SerializeField, Tooltip("Bound meter during pinata.")]
    private PinataMeter meter;

    [Header("Audio")]
    [SerializeField, Tooltip("Sound played whenever the pinata takes a hit.")]
    private SoundData hitSound;
    #endregion

    #region IDamageable
    public bool IsAlive => meter != null && meter.IsEnabled;

    public void TakeDamage(int amount, GameObject source)
    {
        if (!IsAlive) return;

        meter.ApplyHit(amount);

        if (hitSound != null)
        {
            SoundUtils.PlayAtPosition(hitSound, transform.position);
        }
    }
    #endregion

    #region Public API
    public void BindPinataMeter(PinataMeter boundMeter)
    {
        meter = boundMeter;
        gameObject.SetActive(meter != null);
    }

    /// <summary>
    /// Convenience for non-projectile sources to push raw damage.
    /// </summary>
    public void ApplyHit(int damage)
    {
        if (!IsAlive) return;

        meter.ApplyHit(damage);

        if (hitSound != null)
        {
            SoundUtils.PlayAtPosition(hitSound, transform.position);
        }
    }
    #endregion
}
