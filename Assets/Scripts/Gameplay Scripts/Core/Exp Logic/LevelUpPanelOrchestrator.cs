using UnityEngine;

/// <summary>
/// Decoupled hook between ExperienceSystem and PowerupPanelUIController:
/// - Listens to pending-upgrade changes.
/// - When pending > lastPending, plays level-up sound.
/// - When pending > 0, pauses gameplay and opens the panel.
/// - When the player applies a card, consumes one pending upgrade.
/// - If more remain, re-rolls; otherwise, hides panel and resumes gameplay.
/// </summary>
public class LevelUpPanelOrchestrator : MonoBehaviour
{
    #region Inspector Fields

    [Header("References")]
    [SerializeField] private ExperienceSystem experienceSystem;
    [SerializeField] private PowerupPanelUIController powerupPanel;

    [Header("Audio")]
    [SerializeField, Tooltip("Played when the player levels up (EXP reaches threshold).")]
    private SoundData levelUpSound;

    [SerializeField, Tooltip("Played when the level-up panel is shown.")]
    private SoundData panelOpenSound;

    #endregion

    #region Private Fields
    private bool isShowing;
    private int lastPendingUpgrades;
    #endregion

    #region Unity Lifecycle

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

    #endregion

    #region Event Handlers

    private void HandlePendingChanged(int pending)
    {
        // LEVEL-UP SOUND: only play when pending increased
        if (pending > lastPendingUpgrades)
        {
            SoundUtils.Play2D(levelUpSound);
        }

        lastPendingUpgrades = pending;

        // Panel logic
        if (pending > 0 && !isShowing)
            OpenPanel();
    }

    private void HandleCardApplied()
    {
        if (experienceSystem == null) return;

        // Consume one upgrade
        if (!experienceSystem.TryConsumeOnePendingUpgrade())
        {
            Debug.LogWarning("[LevelUpPanelOrchestrator] Tried to consume a pending upgrade but none available.");
        }

        if (experienceSystem.PendingUpgrades > 0)
        {
            // Show next pick immediately
            if (powerupPanel != null)
                powerupPanel.ShowAndRoll();
        }
        else
        {
            // No more upgrades available
            ClosePanel();
        }
    }

    #endregion

    #region Panel Control

    private void OpenPanel()
    {
        if (powerupPanel == null) return;

        // Pause gameplay
        if (PauseManager.Instance != null)
            PauseManager.Instance.StopGameplay();

        isShowing = true;

        // PANEL OPEN SOUND
        SoundUtils.Play2D(panelOpenSound);

        // Show and roll new choices
        powerupPanel.ShowAndRoll();
    }

    private void ClosePanel()
    {
        if (!isShowing) return;

        isShowing = false;

        if (powerupPanel != null)
            powerupPanel.Hide();

        // Unpause gameplay
        if (PauseManager.Instance != null)
            PauseManager.Instance.ResumeGameplay();
    }

    #endregion
}
