using UnityEngine;
/// <summary>
/// Optional decoupled hook: subscribes to level-up and shows your powerup panel, pausing gameplay.
/// Use this only if you don't set the panel reference directly on ExperienceSystem.
/// </summary>
public class LevelUpPanelOrchestrator : MonoBehaviour
{
    [SerializeField] private ExperienceSystem experienceSystem;
    [SerializeField] private PowerupPanelUIController powerupPanel;

    private void Awake()
    {
        if (experienceSystem == null)
            experienceSystem = ExperienceSystem.Instance;

        if (powerupPanel == null)
            Debug.LogWarning("[LevelUpPanelOrchestrator] PowerupPanelUIController not set.");
    }

    private void OnEnable()
    {
        if (experienceSystem != null)
            experienceSystem.OnLeveledUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        if (experienceSystem != null)
            experienceSystem.OnLeveledUp -= HandleLevelUp;
    }

    private void HandleLevelUp(int _)
    {
        if (powerupPanel == null) return;

        if (PauseManager.Instance != null)
            PauseManager.Instance.StopGameplay();

        powerupPanel.ShowAndRoll();
    }
}
