using UnityEngine;
/// <summary>
/// Attach to enemy prefabs. Awards EXACTLY 1 EXP when EnemyHealth dies.
/// Subscribes/unsubscribes on enable/disable (pool-safe).
/// Keeps EXP logic out of EnemyHealth (single responsibility).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyExpOnDeath : MonoBehaviour
{
    [Header("EXP Per Kill")]
    [SerializeField, Min(1)] private int expPerKill = 1; // always 1 per your design

    [Header("Options")]
    [SerializeField] private bool ignoreWhilePaused = true;

    private EnemyHealth health;
    private bool subscribed;

    private void Awake()
    {
        if (!TryGetComponent(out health))
            Debug.LogError("[EnemyExpOnDeath] Missing EnemyHealth component.");
    }

    private void OnEnable()
    {
        if (health != null && !subscribed)
        {
            health.OnDied += HandleDeath;
            subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (health != null && subscribed)
        {
            health.OnDied -= HandleDeath;
            subscribed = false;
        }
    }

    private void HandleDeath(EnemyHealth _)
    {
        if (ignoreWhilePaused && PauseManager.Instance != null && PauseManager.Instance.IsGameplayStopped)
            return;

        if (ExperienceSystem.Instance == null)
        {
            Debug.LogWarning("[EnemyExpOnDeath] No ExperienceSystem in scene.");
            return;
        }

        ExperienceSystem.Instance.AddExp(expPerKill); // 1 per enemy
    }
}
