using UnityEngine;

[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Weapons/Weapon Definition")]
public class WeaponDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique weapon type identifier used in code and for upgrades.")]
    [SerializeField] private WeaponType weaponType = WeaponType.Basic;

    [Tooltip("Display name shown in UI or debug logs.")]
    [SerializeField] private string displayName;

    [Header("Projectile")]
    [Tooltip("The projectile prefab this weapon spawns when firing.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Determines how the projectile moves once fired (Linear or Sine).")]
    [SerializeField] private ProjectileMotionType projectileMotionType = ProjectileMotionType.Linear;

    [Tooltip("How hits from this weapon are counted for charge filling.")]
    [SerializeField] private HitCountPolicy hitCountPolicy = HitCountPolicy.OncePerTargetPerProjectile;

    [Header("Pattern")]
    [Tooltip("Defines how the weapon fires projectiles (StraightLine, AlternatingBurst, FanSequential).")]
    [SerializeField] private WeaponPatternType patternType = WeaponPatternType.StraightLine;

    [Header("Base Stats")]
    [Tooltip("Number of firing cycles per second (e.g., 2 = two bursts per second).")]
    [SerializeField] private float baseFireRatePerSecond = 2f;

    [Tooltip("Number of projectiles fired per trigger.")]
    [SerializeField] private int baseProjectileAmount = 1;

    [Tooltip("Number of enemies or objects a projectile can pierce through before being destroyed.")]
    [SerializeField] private int basePiercing = 0;

    [Tooltip("If true, weapon can fire automatically when the shoot button is held down.")]
    [SerializeField] private bool supportsAutoFire = true;

    [Header("Pattern Settings (shared across all weapons)")]
    [Tooltip("Maximum projectiles that can be fired simultaneously in one horizontal row (used by StraightLine pattern).")]
    [SerializeField] private int horizontalSimultaneousLimit = 10;

    [Tooltip("When projectileAmount exceeds the limit, determines how to resolve overflow: 'Rows' (stack another row) or 'RapidStreak' (fire remaining quickly in sequence).")]
    [SerializeField] private OverflowResolution overflowResolution = OverflowResolution.Rows;

    [Tooltip("Delay between consecutive projectiles when fired sequentially (used in RapidStreak or FanSequential patterns).")]
    [SerializeField] private float sequentialShotIntervalSeconds = 0.05f;

    [Tooltip("Delay between missiles when alternating left/right in burst (used by AlternatingBurst pattern).")]
    [SerializeField] private float alternatingBurstIntervalSeconds = 0.10f;

    [Tooltip("Total cone width in degrees for FanSequential pattern (e.g., 30° means ±15° from center).")]
    [SerializeField] private float maxFanAngleTotalDegrees = 30f;

    // === Properties ===
    public WeaponType WeaponType => weaponType;
    public string DisplayName => displayName;

    public GameObject ProjectilePrefab => projectilePrefab;
    public ProjectileMotionType ProjectileMotionType => projectileMotionType;
    public HitCountPolicy HitCountPolicy => hitCountPolicy;

    public WeaponPatternType PatternType => patternType;

    public float BaseFireRatePerSecond => baseFireRatePerSecond;
    public int BaseProjectileAmount => baseProjectileAmount;
    public int BasePiercing => basePiercing;
    public bool SupportsAutoFire => supportsAutoFire;

    public int HorizontalSimultaneousLimit => horizontalSimultaneousLimit;
    public OverflowResolution OverflowResolution => overflowResolution;
    public float SequentialShotIntervalSeconds => sequentialShotIntervalSeconds;
    public float AlternatingBurstIntervalSeconds => alternatingBurstIntervalSeconds;
    public float MaxFanAngleTotalDegrees => maxFanAngleTotalDegrees;
}
