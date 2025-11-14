using UnityEngine;

/// <summary>
/// Initializes an enemy or boss instance from an EnemyDefinition at spawn time.
/// Looks for EnemyHealth (regular) or BossHealth (boss) on the same GameObject.
/// </summary>
[DisallowMultipleComponent]
public class EnemyInitializer : MonoBehaviour
{
    #region Serialized
    [Header("Definition (optional on prefab)")]
    [SerializeField, Tooltip("Stats & scaling. Can be injected by the spawner.")]
    private EnemyDefinition definition;

    [Header("Fallback (used if Definition is null)")]
    [SerializeField, Min(1), Tooltip("Used only when no definition is provided.")]
    private int fallbackHealth = 20;
    #endregion

    #region Cache
    private EnemyHealth enemyHealth;
    private BossHealth bossHealth;
    #endregion

    #region Unity
    private void Awake()
    {
        TryGetComponent(out enemyHealth);
        TryGetComponent(out bossHealth);
    }
    #endregion

    #region Public API
    /// <summary>
    /// Called by the sequencer after instantiation.
    /// </summary>
    public void InitializeFromSpawn(int levelIndex1Based)
    {
        Debug.LogWarning($"[EnemyInitializer] ASDASDASDASNo EnemyHealth or BossHealth on {name}. Nothing to initialize.");
        int hp = (definition != null)
            ? definition.ComputeMaxHealth(levelIndex1Based)
            : Mathf.Max(1, fallbackHealth);

        if (enemyHealth != null)
        {
            enemyHealth.InitializeHealth(hp);
            return;
        }

        if (bossHealth != null)
        {
            bossHealth.InitializeMaxHealth(hp, resetCurrent: true);
            return;
        }

        Debug.LogWarning($"[EnemyInitializer] No EnemyHealth or BossHealth on {name}. Nothing to initialize.");
    }

    /// <summary>Allows the spawner to inject a definition per spawn.</summary>
    public void SetDefinition(EnemyDefinition def) => definition = def;
    #endregion
}
