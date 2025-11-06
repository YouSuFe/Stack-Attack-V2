using UnityEngine;

/// <summary>
/// Decoupled hook between ExperienceSystem and PowerupPanelUIController:
/// - Listens to pending-upgrade changes.
/// - When pending > 0, pauses gameplay and opens the panel.
/// - When the player applies a card, consumes one pending upgrade.
/// - If more remain, re-rolls; otherwise, hides panel and resumes gameplay.
/// </summary>
public class LevelUpPanelOrchestrator : MonoBehaviour
{
    [SerializeField] private ExperienceSystem experienceSystem;
    [SerializeField] private PowerupPanelUIController powerupPanel;

    private bool isShowing;

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
            experienceSystem.OnPendingUpgradesChanged += HandlePendingChanged;

        if (powerupPanel != null)
            powerupPanel.OnCardApplied += HandleCardApplied;
    }

    private void OnDisable()
    {
        if (experienceSystem != null)
            experienceSystem.OnPendingUpgradesChanged -= HandlePendingChanged;

        if (powerupPanel != null)
            powerupPanel.OnCardApplied -= HandleCardApplied;
    }

    private void HandlePendingChanged(int pending)
    {
        if (pending > 0 && !isShowing)
            OpenPanel();
    }

    private void HandleCardApplied()
    {
        if (experienceSystem == null) return;

        // Player accepted one upgrade: consume it
        if (!experienceSystem.TryConsumeOnePendingUpgrade())
        {
            Debug.LogWarning("[LevelUpPanelOrchestrator] Tried to consume a pending upgrade but none available.");
        }

        if (experienceSystem.PendingUpgrades > 0)
        {
            // Chain next pick immediately
            if (powerupPanel != null)
                powerupPanel.ShowAndRoll();
        }
        else
        {
            // All done for now
            ClosePanel();
        }
    }

    private void OpenPanel()
    {
        if (powerupPanel == null) return;

        if (PauseManager.Instance != null)
            PauseManager.Instance.StopGameplay();

        isShowing = true;
        powerupPanel.ShowAndRoll();
    }

    private void ClosePanel()
    {
        if (!isShowing) return;

        isShowing = false;

        if (powerupPanel != null)
            powerupPanel.Hide();

        if (PauseManager.Instance != null)
            PauseManager.Instance.ResumeGameplay();
    }
}
