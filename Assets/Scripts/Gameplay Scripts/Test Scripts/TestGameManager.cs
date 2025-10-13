using UnityEngine;

/// <summary>
/// Minimal bootstrap & sandbox:
/// - Sets combat ON so the special skill can fire on input release
/// - Equips ALL weapons (tries every WeaponType; ignores ones missing in catalog)
/// - Optionally spawns a set of TestEnemy prefabs for immediate target practice
/// - Prints helpful debug logs for special-skill charge/activate/end
/// </summary>
public class TestGameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private WeaponDriver weaponDriver;
    [SerializeField] private SpecialSkillDriver specialSkillDriver;
    [SerializeField] private WeaponCatalog weaponCatalog;

    [Header("Startup Options")]
    [SerializeField] private bool setCombatOnStart = true;
    [SerializeField] private bool equipAllWeaponsOnStart = true;
    [SerializeField] private bool spawnEnemiesOnStart = true;

    [Header("Enemy Spawning")]
    [SerializeField] private GameObject testEnemyPrefab;   // must have TestEnemy + Collider2D (isTrigger=false)
    [SerializeField] private int spawnCount = 6;
    [SerializeField] private float spawnStartY = 6f;
    [SerializeField] private float spawnXHalfRange = 3.5f;
    [SerializeField] private float verticalSpacing = 1.75f;

    private Transform playerTransform;

    private void Awake()
    {
        if (weaponDriver == null)
            weaponDriver = FindObjectOfType<WeaponDriver>();

        if (specialSkillDriver == null)
            specialSkillDriver = FindObjectOfType<SpecialSkillDriver>();

        if (weaponCatalog == null)
            Debug.LogWarning("[TestGameManager] WeaponCatalog is not assigned (EquipAll will still try each enum).");

        // Find player transform (prefer the mover’s object)
        var mover = FindObjectOfType<PlayerDragMover>();
        if (mover != null) playerTransform = mover.transform;
        else if (weaponDriver != null) playerTransform = weaponDriver.transform;
    }

    private void OnEnable()
    {
        if (specialSkillDriver != null)
        {
            specialSkillDriver.OnChargeChanged += HandleChargeChanged;
            specialSkillDriver.OnSkillActivated += HandleSkillActivated;
            specialSkillDriver.OnSkillEnded += HandleSkillEnded;
        }
    }

    private void OnDisable()
    {
        if (specialSkillDriver != null)
        {
            specialSkillDriver.OnChargeChanged -= HandleChargeChanged;
            specialSkillDriver.OnSkillActivated -= HandleSkillActivated;
            specialSkillDriver.OnSkillEnded -= HandleSkillEnded;
        }
    }

    private void Start()
    {
        if (setCombatOnStart && specialSkillDriver != null)
        {
            specialSkillDriver.SetIsInCombat(true);
            Debug.Log("[TestGameManager] Combat enabled for special skill.");
        }

        if (equipAllWeaponsOnStart && weaponDriver != null)
        {
            int equipped = 0;
            foreach (var raw in System.Enum.GetValues(typeof(WeaponType)))
            {
                var type = (WeaponType)raw;
                var weapon = weaponDriver.Equip(type);   // returns null if not defined in catalog
                if (weapon != null)
                {
                    equipped++;
                    Debug.Log($"[TestGameManager] Equipped weapon: {type}");
                }
            }
            Debug.Log($"[TestGameManager] Total equipped weapons: {equipped}");
        }

        if (spawnEnemiesOnStart && testEnemyPrefab != null && playerTransform != null)
        {
            SpawnTestEnemies();
        }
        else
        {
            Debug.Log("[TestGameManager] Skipping enemy spawn (missing prefab or playerTransform).");
        }
    }

    private void SpawnTestEnemies()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            float x = Random.Range(-spawnXHalfRange, spawnXHalfRange);
            float y = spawnStartY + i * verticalSpacing;
            Vector3 pos = new Vector3(x, y, 0f);

            var go = Instantiate(testEnemyPrefab, pos, Quaternion.identity);
            var testEnemy = go.GetComponent<TestEnemy>();

            if (testEnemy != null)
            {
                // optional: if your TestEnemy exposes Initialize(Transform), call it:
                // testEnemy.Initialize(playerTransform);
                Debug.Log($"[TestGameManager] Spawned TestEnemy at {pos}");
            }
            else
            {
                Debug.LogWarning("[TestGameManager] TestEnemy prefab missing TestEnemy component.");
            }
        }
    }

    // -------- Special-skill debug hooks --------

    private void HandleChargeChanged(int current, int required)
    {
        Debug.Log($"[SpecialSkill] Charge: {current}/{required}");
    }

    private void HandleSkillActivated()
    {
        Debug.Log("[SpecialSkill] Activated.");
    }

    private void HandleSkillEnded()
    {
        Debug.Log("[SpecialSkill] Ended.");
    }
}

