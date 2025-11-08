public enum SpecialSkillType { Laser }

public enum SpecialDamageMode
{
    /// <summary>
    /// Skill deals its damage once (e.g., laser burst, explosion).
    /// </summary>
    SingleImpact,

    /// <summary>
    /// Skill deals damage continuously over time while active.
    /// </summary>
    Continuous
}