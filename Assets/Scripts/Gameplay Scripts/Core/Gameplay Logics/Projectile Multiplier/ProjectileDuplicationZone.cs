using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ProjectileDuplicationZone : MonoBehaviour, IStoppable
{
    #region Serialized Fields
    [Header("Multiplier")]
    [Tooltip("Total number of projectiles AFTER duplication. If set to 4, one incoming projectile becomes 4 (spawns 3 additional).")]
    [SerializeField, Min(2)] private int totalProjectileCountAfterDuplication = 2;


    [Header("Common Offsets")]
    [Tooltip("Small random radial offset (world units) applied to every duplicate so they don’t stack at the exact same position.")]
    [SerializeField, Range(0f, 0.5f)] private float duplicateRadialJitterRadius = 0.08f;


    [Header("Basic (Straight)")]
    [Tooltip("Horizontal spacing (world units) applied left/right for duplicates from Basic projectiles. Aim direction stays EXACTLY the same.")]
    [SerializeField, Range(0f, 0.5f)] private float basicHorizontalSpacing = 0.12f;


    [Header("Kunai (Linear)")]
    [Tooltip("Maximum angle jitter (degrees, ±) applied to duplicates from Kunai projectiles for a subtle fan effect.")]
    [SerializeField, Range(0f, 5f)] private float kunaiDuplicateAngleJitterDegrees = 2.0f;

    [Tooltip("Base lateral spacing (world units) for Kunai duplicates. Scales per duplicate index for clear separation.")]
    [SerializeField, Range(0f, 1f)] private float kunaiLateralBaseSpacing = 0.25f;


    [Header("Missile (Sine)")]
    [Tooltip("Maximum angle jitter (degrees, ±) applied to duplicates from Missiles (adds on top of any jitter set in MissileWeapon).")]
    [SerializeField, Range(0f, 5f)] private float missileDuplicateAngleJitterDegrees = 1.5f;

    [Tooltip("Maximum extra sine phase (radians, ±) applied to Missile duplicates so their sine paths don’t overlap perfectly.")]
    [SerializeField, Range(0f, Mathf.PI)] private float missileDuplicatePhaseJitterRadians = 0.35f;

    [Tooltip("Base lateral spacing (world units) for Missile duplicates. Scales per duplicate index to create nice separation.")]
    [SerializeField, Range(0f, 0.5f)] private float missileLateralBaseSpacing = 0.06f;


    [Header("Services")]
    [Tooltip("Projectile pool service. If not set, it will be found at runtime (FindObjectOfType).")]
    [SerializeField] private ProjectilePoolService poolService;
    #endregion

    #region State
    private bool isStopped; // true while gameplay is paused
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (!poolService)
            poolService = FindFirstObjectByType<ProjectilePoolService>();
    }

    private void OnEnable()
    {
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.Register(this);
            isStopped = PauseManager.Instance.IsGameplayStopped;
        }
    }

    private void OnDisable()
    {
        if (PauseManager.Instance != null)
            PauseManager.Instance.Unregister(this);

        isStopped = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Do nothing while paused
        if (isStopped || (PauseManager.Instance != null && PauseManager.Instance.IsGameplayStopped))
            return;

        if (!other || !other.isActiveAndEnabled)
            return;

        if (!other.TryGetComponent<ProjectileBase>(out var sourceProjectile))
            return;

        if (!sourceProjectile.isActiveAndEnabled)
            return;

        DuplicateFrom(sourceProjectile);
    }
    #endregion

    #region IStoppable
    public void OnStopGameplay() { isStopped = true; }
    public void OnResumeGameplay() { isStopped = false; }
    #endregion

    #region Duplication
    private void DuplicateFrom(ProjectileBase sourceProjectile)
    {
        // Snapshot source transform information once
        Transform sourceTransform = sourceProjectile.transform;
        Vector3 sourceWorldPosition = sourceTransform.position;
        Quaternion sourceWorldRotation = sourceTransform.rotation;
        Vector3 localRight = sourceTransform.right;

        // Read initialization data from the projectile (via properties on ProjectileBase)
        GameObject owner = sourceProjectile.Owner;
        int damageAmount = sourceProjectile.DamageAmount;
        int remainingPiercing = sourceProjectile.RemainingPiercing;
        HitCountPolicy hitPolicy = sourceProjectile.HitCountPolicy;

        // We already have one (the source). Spawn the remaining (N - 1).
        int duplicatesToSpawn = Mathf.Max(0, totalProjectileCountAfterDuplication - 1);

        for (int i = 0; i < duplicatesToSpawn; i++)
        {
            SpawnDuplicate(
                sourceProjectile,
                owner,
                damageAmount,
                remainingPiercing,
                hitPolicy,
                sourceWorldPosition,
                sourceWorldRotation,
                localRight,
                i
            );
        }
    }

    private void SpawnDuplicate(
        ProjectileBase sourceProjectile,
        GameObject owner,
        int damageAmount,
        int remainingPiercing,
        HitCountPolicy hitPolicy,
        Vector3 sourceWorldPosition,
        Quaternion sourceWorldRotation,
        Vector3 localRight,
        int duplicateIndex)
    {
        // Small radial jitter (world space) so duplicates don’t sit exactly on the entry point
        Vector2 randomOnDisk = Random.insideUnitCircle * duplicateRadialJitterRadius;
        Vector3 spawnWorldPosition = sourceWorldPosition + new Vector3(randomOnDisk.x, randomOnDisk.y, 0f);
        Quaternion spawnWorldRotation = sourceWorldRotation;

        switch (sourceProjectile.SourceWeapon)
        {
            case WeaponType.Basic:
                {
                    // Keep aim direction EXACT; spaced horizontally left/right in symmetric steps: 0, +d, -d, +2d, -2d, ...
                    int step = (duplicateIndex / 2) + 1;
                    float sign = (duplicateIndex % 2 == 0) ? 1f : -1f;
                    spawnWorldPosition = sourceWorldPosition + localRight * (basicHorizontalSpacing * step * sign);
                }
                break;

            case WeaponType.Kunai:
                {
                    // Angle jitter for fan feel
                    float angleJitter = Random.Range(-kunaiDuplicateAngleJitterDegrees, kunaiDuplicateAngleJitterDegrees);
                    spawnWorldRotation = sourceWorldRotation * Quaternion.Euler(0f, 0f, angleJitter);

                    // Symmetric lateral spacing like missiles/basic: 0,+d,-d,+2d,-2d,...
                    int step = (duplicateIndex / 2) + 1;
                    float sign = (duplicateIndex % 2 == 0) ? 1f : -1f;
                    spawnWorldPosition += localRight * (kunaiLateralBaseSpacing * step * sign);
                }
                break;

            case WeaponType.Missile:
                {
                    // Subtle aim jitter (degrees) + lateral spacing that grows with index
                    float angleJitter = Random.Range(-missileDuplicateAngleJitterDegrees, missileDuplicateAngleJitterDegrees);
                    spawnWorldRotation = sourceWorldRotation * Quaternion.Euler(0f, 0f, angleJitter);

                    int step = (duplicateIndex / 2) + 1;
                    float sign = (duplicateIndex % 2 == 0) ? 1f : -1f;
                    spawnWorldPosition += localRight * (missileLateralBaseSpacing * step * sign);
                }
                break;

            default:
                // Unknown weapon type: keep pose; only radial jitter applied above
                break;
        }

        // Spawn via pool using the same prefab key as the source (fallback to instantiate if service missing)
        ProjectileBase duplicate =
            poolService != null
                ? poolService.SpawnLike(sourceProjectile, spawnWorldPosition, spawnWorldRotation, null)
                : InstantiateFallback(sourceProjectile, spawnWorldPosition, spawnWorldRotation);

        // Initialize exactly like the source (no interface changes required)
        if (duplicate is IProjectile duplicateIProjectile)
            duplicateIProjectile.Initialize(owner, damageAmount, remainingPiercing, hitPolicy);

        // Preserve the weapon tag so downstream logic knows where it came from
        duplicate.SetSourceWeapon(sourceProjectile.SourceWeapon);

        // Missile-only polish: add small sine phase offset so paths don’t overlap perfectly
        if (duplicate is SineMissileProjectile sineMissile)
        {
            float deltaPhase = Random.Range(-missileDuplicatePhaseJitterRadians, missileDuplicatePhaseJitterRadians);
            sineMissile.AddPhaseOffset(deltaPhase);
        }
    }

    private static ProjectileBase InstantiateFallback(ProjectileBase template, Vector3 position, Quaternion rotation)
    {
        ProjectileBase instance = Instantiate(template);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);
        return instance;
    }
    #endregion
}
