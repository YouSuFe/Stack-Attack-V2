using UnityEngine;

/// <summary>
/// A sine-motion projectile that also rotates randomly around its Z-axis each spawn.
/// </summary>
public class SineMissileProjectile : ProjectileBase
{
    #region Motion Settings
    [Header("Motion")]
    [SerializeField, Tooltip("Forward speed in units per second.")]
    private float forwardSpeed = 8f;

    [SerializeField, Tooltip("Amplitude of sine wave motion.")]
    private float sineAmplitude = 0.7f;

    [SerializeField, Tooltip("Frequency of sine wave motion (Hz).")]
    private float sineFrequencyHz = 2.5f;

    [SerializeField, Tooltip("Phase offset in radians (0 for right, π for left to mirror).")]
    private float phaseOffsetRadians = 0f;
    #endregion

    #region Rotation Settings
    [Header("Rotation")]
    [SerializeField, Tooltip("Minimum rotation speed in degrees per second.")]
    private float minRotationSpeed = 200f;

    [SerializeField, Tooltip("Maximum rotation speed in degrees per second.")]
    private float maxRotationSpeed = 600f;
    #endregion

    #region Private Fields
    private float elapsed;
    private Vector3 startPosition;
    private Vector2 forwardDirection;
    private Vector2 perpendicularDirection;

    private float rotationSpeed; // random spin speed (+/-)
    #endregion

    #region Pool Lifecycle
    public override void OnSpawnFromPool()
    {
        base.OnSpawnFromPool();

        // Reset sine wave parameters
        elapsed = 0f;
        startPosition = transform.position;
        forwardDirection = (Vector2)transform.up.normalized;
        perpendicularDirection = new Vector2(-forwardDirection.y, forwardDirection.x);

        // Initialize random rotation each time object is spawned
        float speed = Random.Range(minRotationSpeed, maxRotationSpeed);
        rotationSpeed = (Random.value < 0.5f ? -speed : speed);
    }
    #endregion

    #region Projectile Update
    protected override void TickMotion(float deltaTime)
    {
        // --- Sine-wave forward motion ---
        elapsed += deltaTime;
        float forwardDist = forwardSpeed * elapsed;
        float lateral = sineAmplitude * Mathf.Sin(2f * Mathf.PI * sineFrequencyHz * elapsed + phaseOffsetRadians);
        Vector2 position = (Vector2)startPosition + forwardDirection * forwardDist + perpendicularDirection * lateral;
        transform.position = position;

        // --- Continuous spin around Z-axis ---
        transform.Rotate(0f, 0f, rotationSpeed * deltaTime, Space.Self);
    }
    #endregion

    #region Public API
    public void SetPhaseOffsetRadians(float radians)
    {
        phaseOffsetRadians = radians;
    }

    public void AddPhaseOffset(float delta)
    {
        phaseOffsetRadians += delta;
    }
    #endregion
}
