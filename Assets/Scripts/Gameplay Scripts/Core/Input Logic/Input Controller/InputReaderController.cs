// InputReaderController.cs
using UnityEngine;

/// <summary>
/// Centralized input controller for the player:
/// - Enables and disables the InputReader actions.
/// - Forwards drag start/update/end to PlayerDragMover for movement.
/// - Controls WeaponDriver: press -> fire once + start auto, release -> stop.
/// - Adds an explicit "Input Policy" so you can allow only movement, or enable/disable
///   primary fire and special skill independently (even while not paused).
/// 
/// Pause Notes:
/// - When paused (OnStopGameplay), inputs are disabled and any ongoing drag/auto-fire is stopped.
/// - On resume (OnResumeGameplay), the previous enabled state is restored and your policy remains in effect.
/// </summary>
public class InputReaderController : MonoBehaviour, IStoppable
{
    #region Serialized References
    [Header("References")]
    [SerializeField, Tooltip("ScriptableObject that wraps the Unity Input System actions & raises events.")]
    private InputReader inputReader;

    [SerializeField, Tooltip("Handles movement by pointer drag (Begin/Update/End).")]
    private PlayerDragMover dragMover;

    [SerializeField, Tooltip("Handles weapon firing (single + auto).")]
    private WeaponDriver weaponDriver;

    [SerializeField, Tooltip("Handles special skill trigger logic (fires on release in your setup).")]
    private SpecialSkillDriver specialSkillDriver;
    #endregion

    #region Input Policy (Toggles)
    [Header("Input Policy (Runtime Toggles)")]
    [SerializeField, Tooltip("If false, player cannot move via drag. Any active drag is force-ended when toggled off.")]
    private bool allowMovement = true;

    [SerializeField, Tooltip("If false, primary fire input is ignored. Also gates WeaponDriver canAttack.")]
    private bool allowPrimaryFire = true;

    [SerializeField, Tooltip("If false, special skill input (on release) is ignored. Also toggles SpecialSkillDriver combat flag.")]
    private bool allowSpecial = true;
    #endregion

    #region Private State
    private bool inputsEnabled;                   // Current input state for this controller
    private bool inputsWereEnabledBeforeStop;     // For pause/resume bookkeeping
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Optional safety checks – you can remove if you prefer to assign by inspector only.
        if (inputReader == null) Debug.LogWarning("[InputReaderController] InputReader is not assigned.");
        if (dragMover == null) Debug.LogWarning("[InputReaderController] PlayerDragMover is not assigned.");
        if (weaponDriver == null) Debug.LogWarning("[InputReaderController] WeaponDriver is not assigned.");
        if (specialSkillDriver == null) Debug.LogWarning("[InputReaderController] SpecialSkillDriver is not assigned.");

        // Sync dependent systems to initial policy at startup
        SyncSystemsToPolicy();
    }

    private void OnEnable()
    {
        RegisterInputEvents();
        EnableInputs(); // Opt-in default; if you want inputs off at boot, call DisableInputs() in Start.
    }

    private void OnDisable()
    {
        UnregisterInputEvents();
        DisableInputs();
    }
    #endregion

    #region Event Registration
    private void RegisterInputEvents()
    {
        if (inputReader == null) return;

        inputReader.OnDragStarted += HandleDragStarted;
        inputReader.OnDrag += HandleDrag;
        inputReader.OnDragEnded += HandleDragEnded;
    }

    private void UnregisterInputEvents()
    {
        if (inputReader == null) return;

        inputReader.OnDragStarted -= HandleDragStarted;
        inputReader.OnDrag -= HandleDrag;
        inputReader.OnDragEnded -= HandleDragEnded;
    }
    #endregion

    #region Input Enable/Disable
    /// <summary>
    /// Enable input actions and start receiving events.
    /// </summary>
    public void EnableInputs()
    {
        if (inputsEnabled || inputReader == null) return;

        inputReader.EnableInput();
        inputsEnabled = true;
    }

    /// <summary>
    /// Disable input actions and stop receiving events.
    /// </summary>
    public void DisableInputs()
    {
        if (!inputsEnabled || inputReader == null) return;

        inputReader.DisableInput();
        inputsEnabled = false;
    }
    #endregion

    #region Input Handlers
    private void HandleDragStarted(Vector2 screenPosition)
    {
        // Movement
        if (allowMovement && dragMover != null)
            dragMover.BeginDrag(screenPosition);

        // Primary Fire (press)
        if (allowPrimaryFire && weaponDriver != null)
            weaponDriver.OnShootPressed(true);
    }

    private void HandleDrag(Vector2 screenPosition)
    {
        // Movement
        if (allowMovement && dragMover != null)
            dragMover.UpdateDrag(screenPosition);
    }

    private void HandleDragEnded(Vector2 screenPosition)
    {
        // Special (your special fires on release in current design)
        if (allowSpecial && specialSkillDriver != null)
            specialSkillDriver.NotifyShootInputReleased();

        // Movement
        if (allowMovement && dragMover != null)
            dragMover.EndDrag(screenPosition);

        // Primary Fire (release) - tell weapons to stop regardless (safe/no-op if disallowed)
        if (weaponDriver != null)
            weaponDriver.OnShootPressed(false);
    }
    #endregion

    #region Policy API (Public)
    /// <summary>
    /// Allow only movement (no primary fire, no special).
    /// </summary>
    public void SetProfile_MovementOnly()
    {
        SetAllowMovement(true);
        SetAllowPrimaryFire(false);
        SetAllowSpecial(false);
    }

    /// <summary>
    /// Enable everything (movement + primary fire + special).
    /// </summary>
    public void SetProfile_AllEnabled()
    {
        SetAllowMovement(true);
        SetAllowPrimaryFire(true);
        SetAllowSpecial(true);
    }

    /// <summary>
    /// Fine-grained toggle: Movement by drag.
    /// </summary>
    public void SetAllowMovement(bool value)
    {
        if (allowMovement == value) return;
        allowMovement = value;

        if (!allowMovement && dragMover != null)
            dragMover.ForceEndDrag(); // ensure we don't keep sliding when movement is turned off
    }

    /// <summary>
    /// Fine-grained toggle: Primary fire input and weapon driver gate.
    /// </summary>
    public void SetAllowPrimaryFire(bool value)
    {
        if (allowPrimaryFire == value) return;
        allowPrimaryFire = value;

        // Gate weapons at the source as well for safety
        if (weaponDriver != null)
        {
            weaponDriver.SetCanAttack(allowPrimaryFire);

            // If disabling, immediately stop any auto-fire
            if (!allowPrimaryFire)
                weaponDriver.OnShootPressed(false);
        }
    }

    /// <summary>
    /// Fine-grained toggle: Special skill on release.
    /// Also sets combat state on the SpecialSkillDriver (optional but useful).
    /// </summary>
    public void SetAllowSpecial(bool value)
    {
        if (allowSpecial == value) return;
        allowSpecial = value;

        if (specialSkillDriver != null)
            specialSkillDriver.SetIsInCombat(allowSpecial);
    }
    #endregion

    #region IStoppable (Pause/Resume)
    /// <summary>
    /// Called by PauseManager (or any system) to stop gameplay.
    /// We: 1) remember input enabled state, 2) stop firing & end drags, 3) disable inputs.
    /// </summary>
    public void OnStopGameplay()
    {
        inputsWereEnabledBeforeStop = inputsEnabled;

        // Safety: stop firing and end any drag cleanly
        if (weaponDriver != null) weaponDriver.OnShootPressed(false);
        if (dragMover != null) dragMover.ForceEndDrag();

        // Block new input events during pause/UI
        DisableInputs();
    }

    /// <summary>
    /// Called by PauseManager (or any system) to resume gameplay.
    /// We restore input state to what it was before the stop.
    /// </summary>
    public void OnResumeGameplay()
    {
        if (inputsWereEnabledBeforeStop)
            EnableInputs();
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Apply current policy to underlying systems on startup or when refs are assigned.
    /// </summary>
    private void SyncSystemsToPolicy()
    {
        // Movement has no global gate, but if disabled we ensure no active drag
        if (!allowMovement && dragMover != null)
            dragMover.ForceEndDrag();

        if (weaponDriver != null)
        {
            weaponDriver.SetCanAttack(allowPrimaryFire);
            if (!allowPrimaryFire) weaponDriver.OnShootPressed(false);
        }

        if (specialSkillDriver != null)
        {
            specialSkillDriver.SetIsInCombat(allowSpecial);
        }
    }
    #endregion
}
