using UnityEngine;

public enum StatScalingMode
{
    None,        // Always base value
    Linear,      // base * (1 + linearPerLevel * (level-1))
    Exponential, // base * pow(expFactor, level-1)
    Curve        // base * curve.Evaluate(level) where level is 1-based
}

[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Game/Enemy/Definition")]
public class EnemyDefinition : ScriptableObject
{
    #region Base Stats
    [Header("Base Stats")]
    [SerializeField, Min(1), Tooltip("Base HP at level 1.")]
    private int baseHealth = 20;
    #endregion

    #region Scaling
    [Header("Health Scaling")]
    [SerializeField, Tooltip("How HP scales with level.")]
    private StatScalingMode healthScaling = StatScalingMode.Linear;

    [SerializeField, Tooltip("Used when Linear: 1.0 doubles per level, 0.5 adds +50% per level.")]
    private float linearPerLevel = 1f;

    [SerializeField, Tooltip("Used when Exponential: 1.5 => +50% per level.")]
    private float expFactor = 1.5f;

    [SerializeField, Tooltip("Used when Curve: X=Level (start at 1), Y=Multiplier.")]
    private AnimationCurve healthCurve = AnimationCurve.Linear(1f, 1f, 10f, 3f);

    [Header("Clamp (Optional)")]
    [SerializeField] private int minHealth = 1;
    [SerializeField] private int maxHealth = 999999;
    #endregion

    #region Public API
    /// <summary>
    /// Returns the computed Max HP for a given 1-based level.
    /// </summary>
    public int ComputeMaxHealth(int level)
    {
        level = Mathf.Max(1, level);
        float multiplier = 1f;

        switch (healthScaling)
        {
            case StatScalingMode.None:
                multiplier = 1f;
                break;

            case StatScalingMode.Linear:
                multiplier = 1f + Mathf.Max(0f, linearPerLevel) * (level - 1);
                break;

            case StatScalingMode.Exponential:
                multiplier = Mathf.Pow(Mathf.Max(1f, expFactor), level - 1);
                break;

            case StatScalingMode.Curve:
                multiplier = Mathf.Max(0f, healthCurve.Evaluate(level));
                break;
        }

        int hp = Mathf.RoundToInt(baseHealth * multiplier);
        return Mathf.Clamp(hp, Mathf.Max(1, minHealth), Mathf.Max(minHealth, maxHealth));
    }
    #endregion
}
