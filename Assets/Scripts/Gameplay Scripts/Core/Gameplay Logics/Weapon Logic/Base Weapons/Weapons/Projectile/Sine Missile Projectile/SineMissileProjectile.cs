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

    public override void OnSpawnFromPool()
    {
        base.OnSpawnFromPool();
        elapsed = 0f;
        startPosition = transform.position;
        forwardDirection = (Vector2)transform.up.normalized;
        perpendicularDirection = new Vector2(-forwardDirection.y, forwardDirection.x);
    }


    protected override void TickMotion(float deltaTime)
    {
        elapsed += deltaTime;
        float forwardDist = forwardSpeed * elapsed;
        float lateral = sineAmplitude * Mathf.Sin(2f * Mathf.PI * sineFrequencyHz * elapsed + phaseOffsetRadians);
        Vector2 position = (Vector2)startPosition + forwardDirection * forwardDist + perpendicularDirection * lateral;
        transform.position = position;
    }

    public void SetPhaseOffsetRadians(float radians)
    {
        phaseOffsetRadians = radians;
    }

    public void AddPhaseOffset(float delta)
    {
        phaseOffsetRadians += delta;
    }
}


