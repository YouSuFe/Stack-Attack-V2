using UnityEngine;
using UnityEngine.UI;
using PixeLadder.EasyTransition;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// PauseUIController
/// - Clicking the HUD "Pause" button pauses gameplay and shows the Pause UI root.
/// - "Resume" button resumes gameplay and hides the Pause UI root.
/// - "Main Menu" button transitions to the menu scene with a random fade effect.
/// </summary>
/// TODo: add Stage X dynamic UI.
public class PauseUIController : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField, Tooltip("Pause manager that controls gameplay pause/resume.")]
    private PauseManager pauseManager;

    [SerializeField, Tooltip("HUD button that the player taps to open the pause menu.")]
    private Button pauseToggleButton;

    [SerializeField, Tooltip("Root GameObject of the Pause UI (shown when paused).")]
    private GameObject pauseUiRoot;

    [Header("Pause UI Buttons")]
    [SerializeField, Tooltip("Button inside Pause UI to resume gameplay.")]
    private Button resumeButton;

    [SerializeField, Tooltip("Button inside Pause UI to go back to Main Menu.")]
    private Button goToMenuButton;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (pauseToggleButton != null)
            pauseToggleButton.onClick.AddListener(OnPauseToggleClicked);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeClicked);

        if (goToMenuButton != null)
            goToMenuButton.onClick.AddListener(OnGoToMenuClicked);

        // Ensure initial UI matches current pause state
        ApplyVisualState(pauseManager != null && pauseManager.IsGameplayStopped);
    }

    private void OnDisable()
    {
        if (pauseToggleButton != null)
            pauseToggleButton.onClick.RemoveListener(OnPauseToggleClicked);

        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(OnResumeClicked);

        if (goToMenuButton != null)
            goToMenuButton.onClick.RemoveListener(OnGoToMenuClicked);
    }
    #endregion

    #region Button Handlers
    private void OnPauseToggleClicked()
    {
        if (pauseManager == null) return;

        // Always pause & open the menu when tapping the HUD pause button
        if (!pauseManager.IsGameplayStopped)
        {
            pauseManager.StopGameplay();
            ApplyVisualState(true);
        }
        // If already paused, you can decide to no-op or also open (it should already be open)
        // We'll just ensure it's visible.
        else
        {
            ApplyVisualState(true);
        }
    }

    private void OnResumeClicked()
    {
        if (pauseManager == null) return;

        if (pauseManager.IsGameplayStopped)
            pauseManager.ResumeGameplay();

        ApplyVisualState(false);
    }

    private void OnGoToMenuClicked()
    {
        // Optional: ensure gameplay is paused while transitioning
        if (pauseManager != null && !pauseManager.IsGameplayStopped)
            pauseManager.StopGameplay();

        SceneTransitioner.Instance.LoadScene(SceneNames.MainMenu);
    }
    #endregion

    #region Helpers
    private void ApplyVisualState(bool paused)
    {
        if (pauseUiRoot != null)
            pauseUiRoot.SetActive(paused);

        // Optional UX: disable the HUD pause button while the menu is up
        if (pauseToggleButton != null)
            pauseToggleButton.interactable = !paused;
    }
    #endregion
}
