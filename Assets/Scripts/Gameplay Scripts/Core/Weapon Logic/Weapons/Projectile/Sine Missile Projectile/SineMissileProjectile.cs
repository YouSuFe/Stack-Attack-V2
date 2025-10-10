using UnityEngine;

public class SineMissileProjectile : ProjectileBase
{
    [Header("Motion")]
    [SerializeField] private float forwardSpeed = 8f;
    [SerializeField] private float sineAmplitude = 0.7f;
    [SerializeField] private float sineFrequencyHz = 2.5f;

    // Phase offset in radians (0 for right, π for left to mirror)
    [SerializeField] private float phaseOffsetRadians = 0f;

    private float elapsed;
    private Vector3 startPosition;
    private Vector2 forwardDirection;
    private Vector2 perpendicularDirection;

    protected override void Awake()
    {
        base.Awake();
        startPosition = transform.position;

        // Capture initial world directions from local up
        forwardDirection = (Vector2)transform.up.normalized;
        perpendicularDirection = new Vector2(-forwardDirection.y, forwardDirection.x); // 90° left
    }

    protected override void TickMotion(float dt)
    {
        elapsed += dt;

        float forwardDist = forwardSpeed * elapsed;
        float lateral = sineAmplitude * Mathf.Sin(2f * Mathf.PI * sineFrequencyHz * elapsed + phaseOffsetRadians);

        Vector2 pos = (Vector2)startPosition + forwardDirection * forwardDist + perpendicularDirection * lateral;
        transform.position = pos;
    }

    public void SetPhaseOffsetRadians(float radians)
    {
        phaseOffsetRadians = radians;
    }
}


