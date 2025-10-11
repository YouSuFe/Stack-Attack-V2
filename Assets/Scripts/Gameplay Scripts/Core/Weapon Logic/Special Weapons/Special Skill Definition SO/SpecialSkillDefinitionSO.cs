using UnityEngine;

[CreateAssetMenu(fileName = "SpecialSkillDefinition", menuName = "Weapons/Special Skill Definition")]
public class SpecialSkillDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private SpecialSkillType specialSkillType = SpecialSkillType.Laser;

    [Header("Charge (hit-based)")]
    [Tooltip("How many hits are required to fill the bar.")]
    [SerializeField] private int requiredCharge = 25;

    [Header("Activation / Duration")]
    [Tooltip("How long visuals/effect stay active after firing (seconds).")]
    [SerializeField] private float activeDurationSeconds = 1.0f;

    [Header("Damage Mode")]
    [Tooltip("SingleImpact = 1-time hit on activation; Continuous = damage ticks while active.")]
    [SerializeField] private SpecialDamageMode damageMode = SpecialDamageMode.SingleImpact;

    [Tooltip("Damage dealt per tick. For SingleImpact, this is the one-time hit value.")]
    [SerializeField] private int damagePerTick = 20;

    [Tooltip("Seconds between damage ticks when in Continuous mode (default 1.0s).")]
    [SerializeField] private float tickIntervalSeconds = 1.0f;

    [Header("Laser Geometry & Targeting")]
    [Tooltip("Max length of the laser.")]
    [SerializeField] private float maxRange = 12f;

    [Tooltip("Beam 'thickness' for 2D hit testing.")]
    [SerializeField] private float beamRadius = 0.15f;

    [Tooltip("Which layers the laser can damage.")]
    [SerializeField] private LayerMask damageMask;

    [Header("Visuals (optional)")]
    [Tooltip("Prefab with LineRenderer/VFX. Can be null.")]
    [SerializeField] private GameObject beamVisualPrefab;

    [Header("Charging Behavior")]
    [Tooltip("If true, each enemy contributes at most +1 charge per activation.\n" +
             "If false, the same enemy can add charge on every Continuous tick (faster refill).")]
    [SerializeField] private bool countOncePerActivation = true;

    // Getters
    public SpecialSkillType SpecialSkillType => specialSkillType;
    public int RequiredCharge => requiredCharge;
    public float ActiveDurationSeconds => activeDurationSeconds;
    public SpecialDamageMode DamageMode => damageMode;
    public int DamagePerTick => damagePerTick;
    public float TickIntervalSeconds => Mathf.Max(0.05f, tickIntervalSeconds);
    public float MaxRange => maxRange;
    public float BeamRadius => beamRadius;
    public LayerMask DamageMask => damageMask;
    public GameObject BeamVisualPrefab => beamVisualPrefab;
    public bool CountOncePerActivation => countOncePerActivation;
}
