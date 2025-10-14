// InputReaderController.cs
using UnityEngine;

/// <summary>
/// Centralized input controller for the player:
/// - Enables and disables the InputReader actions.
/// - Forwards drag start/update/end to PlayerDragMover for movement.
/// - Controls WeaponDriver: press -> fire once + start auto, release -> stop.
/// </summary>
public class InputReaderController : MonoBehaviour, IStoppable
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;     // ScriptableObject
    [SerializeField] private PlayerDragMover dragMover;   // Player movement logic
    [SerializeField] private WeaponDriver weaponDriver;   // Shooting logic
    [SerializeField] private SpecialSkillDriver specialSkillDriver;

    private bool isSubscribed;
    private bool inputsWereEnabledBeforeStop;

    private void OnEnable()
    {
        if (inputReader == null || dragMover == null || weaponDriver == null)
        {
            Debug.LogError("InputReaderController: Missing reference(s).");
            return;
        }

        PauseManager.Instance?.Register(this);
        EnableInputs();
    }

    private void OnDisable()
    {
        PauseManager.Instance?.Unregister(this);
        DisableInputs();
    }

    /// <summary>
    /// Enables input actions and subscribes to drag events.
    /// </summary>
    public void EnableInputs()
    {
        if (inputReader == null || isSubscribed) return;

        inputReader.EnableInput();

        inputReader.OnDragStarted += HandleDragStarted;
        inputReader.OnDrag += HandleDrag;
        inputReader.OnDragEnded += HandleDragEnded;

        isSubscribed = true;
    }

    /// <summary>
    /// Disables input actions and unsubscribes from events.
    /// </summary>
    public void DisableInputs()
    {
        if (inputReader == null || !isSubscribed) return;

        inputReader.OnDragStarted -= HandleDragStarted;
        inputReader.OnDrag -= HandleDrag;
        inputReader.OnDragEnded -= HandleDragEnded;

        inputReader.DisableInput();
        isSubscribed = false;
    }

    // ------------------------
    // Event Handlers
    // ------------------------

    private void HandleDragStarted(Vector2 screenPosition)
    {
        // Begin movement drag
        dragMover.BeginDrag(screenPosition);

        // Start shooting
        weaponDriver.OnShootPressed(true);
    }

    private void HandleDrag(Vector2 screenPosition)
    {
        // Continue moving with finger
        dragMover.UpdateDrag(screenPosition);
        // No need to repeatedly tell WeaponDriver — it’s already auto-firing.
    }

    private void HandleDragEnded(Vector2 screenPosition)
    {
        // Fire special skill if ready
        specialSkillDriver.NotifyShootInputReleased();

        // End movement drag
        dragMover.EndDrag(screenPosition);

        // Stop shooting
        weaponDriver.OnShootPressed(false);
    }

    // ------------------------
    // IStoppable
    // ------------------------

    public void OnStopGameplay()
    {
        // Remember whether inputs were enabled to restore later
        inputsWereEnabledBeforeStop = isSubscribed;

        // Stop firing and end any drag cleanly
        weaponDriver.OnShootPressed(false);
        dragMover.ForceEndDrag();

        // Block new input events during UI
        DisableInputs();
    }

    public void OnResumeGameplay()
    {
        if (inputsWereEnabledBeforeStop)
            EnableInputs();
    }
}
