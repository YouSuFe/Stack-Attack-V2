using UnityEngine;

[DisallowMultipleComponent]
public class ProjectilePoolOutcomeListener : MonoBehaviour
{
    [SerializeField, Tooltip("Projectile pool service to flush on level end.")]
    private ProjectilePoolService poolService;

    [SerializeField, Tooltip("If not set, will use LevelContextBinder.Instance.")]
    private LevelContextBinder binder;

    private void Awake()
    {
        if (!binder) binder = LevelContextBinder.Instance; // convenience
        if (!poolService) poolService = FindFirstObjectByType<ProjectilePoolService>();
    }

    private void OnEnable()
    {
        if (!binder) return;
        binder.OnLevelFailed += HandleEnd;
        binder.OnLevelSucceeded += HandleEnd;
    }

    private void OnDisable()
    {
        if (!binder) return;
        binder.OnLevelFailed -= HandleEnd;
        binder.OnLevelSucceeded -= HandleEnd;
    }

    private void HandleEnd()
    {
        if (poolService != null)
            poolService.DespawnAllActive();
    }
}

