public struct SpawnInitContext
{
    public WeaponDriver WeaponDriver;
    public SpecialSkillDriver SpecialSkillDriver;
    public ProjectilePoolService ProjectilePoolService;
}

public interface IInitializableFromContext
{
    void Initialize(SpawnInitContext context);
}
