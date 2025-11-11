using UnityEngine;

public class GameplayInitializer : MonoBehaviour
{
    public static GameplayInitializer Instance { get; private set; }

    [Header("Drivers")]
    [SerializeField] private WeaponDriver weaponDriver;
    [SerializeField] private SpecialSkillDriver specialSkillDriver;

    [Header("Systems")]
    [SerializeField] private ProjectilePoolService projectilePoolService;


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ResolveMissingReferences();
    }

    public SpawnInitContext GetContext()
    {
        ResolveMissingReferences();
        return new SpawnInitContext
        {
            WeaponDriver = weaponDriver,
            SpecialSkillDriver = specialSkillDriver,
            ProjectilePoolService = projectilePoolService
        };
    }

    private void ResolveMissingReferences()
    {
        if (!weaponDriver) weaponDriver = FindFirstObjectByType<WeaponDriver>();
        if (!specialSkillDriver) specialSkillDriver = FindFirstObjectByType<SpecialSkillDriver>();
        if (!projectilePoolService) projectilePoolService = FindFirstObjectByType<ProjectilePoolService>();
    }
}
