using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates which end-of-level UI is visible:
/// - Listens to LevelContextBinder for success/fail.
/// - On success: shows Success result UI.
/// - On fail: shows Dead screen; if player declines or timer hits 0 → shows Failure result UI.
/// Ensures only one UI is visible at a time.
/// </summary>
[DisallowMultipleComponent]
public class GameUIFlowController : MonoBehaviour
{
    #region Serialized References
    [Header("Controllers")]
    [SerializeField, Tooltip("Controls Success/Failure result groups.")]
    private ResultUIController resultUIController;

    [SerializeField, Tooltip("Dead (KO) canvas with revive / not-revive and countdown.")]
    private DeadCanvasController deadCanvas;
    #endregion

    #region Optional Data (for quick testing)
    [Header("Optional Rewards (Inspector Testing)")]
    [SerializeField, Tooltip("Rewards to display on Success (optional).")]
    private List<ResultViewBase.RewardEntry> successRewardsPreview = new List<ResultViewBase.RewardEntry>();

    [SerializeField, Tooltip("Rewards to display on Failure (optional).")]
    private List<ResultViewBase.RewardEntry> failureRewardsPreview = new List<ResultViewBase.RewardEntry>();

    [SerializeField, Tooltip("If set >=0, Failure UI will show REACHED {value}%. Leave -1 to keep whatever text is already on the component.")]
    private int reachedPercentOnFailure = -1;

    [Header("Dead Canvas Settings")]
    [SerializeField, Tooltip("Seconds to count down on Dead Canvas when shown (overrides DeadCanvas default if >0).")]
    private int deadCountdownSecondsOverride = 0;
    #endregion

    #region Private
    private LevelContextBinder binder;
    private bool showingDead;
    #endregion

    #region Unity
    private void OnEnable()
    {
        // Bind to the current LevelContextBinder (scene-local singleton)
        binder = LevelContextBinder.Instance;
        if (binder != null)
        {
            binder.OnLevelSucceeded += HandleLevelSucceeded;
            binder.OnLevelFailed += HandleLevelFailed;
        }

        // Hook dead canvas decisions
        if (deadCanvas != null)
        {
            deadCanvas.ReviveRequested += HandleReviveChosen;
            deadCanvas.NotReviveRequested += HandleNotReviveChosen;
        }
    }

    private void OnDisable()
    {
        if (binder != null)
        {
            binder.OnLevelSucceeded -= HandleLevelSucceeded;
            binder.OnLevelFailed -= HandleLevelFailed;
            binder = null;
        }

        if (deadCanvas != null)
        {
            deadCanvas.ReviveRequested -= HandleReviveChosen;
            deadCanvas.NotReviveRequested -= HandleNotReviveChosen;
        }
    }
    #endregion

    #region Public API (optional setters if you build rewards at runtime)
    public void SetSuccessRewards(IEnumerable<ResultViewBase.RewardEntry> rewards)
    {
        successRewardsPreview = rewards != null ? new List<ResultViewBase.RewardEntry>(rewards) : new List<ResultViewBase.RewardEntry>();
    }

    public void SetFailureRewards(IEnumerable<ResultViewBase.RewardEntry> rewards)
    {
        failureRewardsPreview = rewards != null ? new List<ResultViewBase.RewardEntry>(rewards) : new List<ResultViewBase.RewardEntry>();
    }

    /// <summary>Set reached percent to show on Failure UI (e.g., 17). Pass -1 to keep existing label.</summary>
    public void SetReachedPercentOnFailure(int value)
    {
        reachedPercentOnFailure = value;
    }
    #endregion

    #region Level Outcomes
    private void HandleLevelSucceeded()
    {
        Debug.Log("[GameUIFlowController] Outcome: SUCCESS → Show Success UI");
        HideDeadCanvasIfNeeded();
        ShowSuccessUI();
    }

    private void HandleLevelFailed()
    {
        Debug.Log("[GameUIFlowController] Outcome: FAIL → Show Dead Canvas");
        HideAllResultGroups();
        ShowDeadCanvas();
    }
    #endregion

    #region Dead Canvas Reactions
    private void HandleReviveChosen()
    {
        // You can put revive logic here (resume gameplay, consume currency, ad flow, etc.)
        Debug.Log("[GameUIFlowController] DeadCanvas: Revive chosen.");
        HideDeadCanvasIfNeeded();
        // No result UI shown; gameplay resumes.
    }

    private void HandleNotReviveChosen()
    {
        Debug.Log("[GameUIFlowController] DeadCanvas: Not Revive (or timeout) → Show Failure UI");
        HideDeadCanvasIfNeeded();
        ShowFailureUI();
    }
    #endregion

    #region Show/Hide Helpers
    private void ShowDeadCanvas()
    {
        if (deadCanvas == null)
        {
            Debug.LogWarning("[GameUIFlowController] DeadCanvasController is not assigned.");
            return;
        }

        showingDead = true;

        // Determine countdown seconds
        if (deadCountdownSecondsOverride > 0)
            deadCanvas.Show(deadCountdownSecondsOverride);
        else
            deadCanvas.Show(); // uses its own default
    }

    private void HideDeadCanvasIfNeeded()
    {
        if (!showingDead || deadCanvas == null) return;
        showingDead = false;
        deadCanvas.Hide();
    }

    private void HideAllResultGroups()
    {
        if (resultUIController != null)
            resultUIController.HideAll();
    }

    private void ShowSuccessUI()
    {
        if (resultUIController == null)
        {
            Debug.LogWarning("[GameUIFlowController] ResultUIController is not assigned.");
            return;
        }

        var rewards = successRewardsPreview != null ? successRewardsPreview : new List<ResultViewBase.RewardEntry>();
        resultUIController.ShowSuccessUIWithSequence(rewards);
    }

    private void ShowFailureUI()
    {
        if (resultUIController == null)
        {
            Debug.LogWarning("[GameUIFlowController] ResultUIController is not assigned.");
            return;
        }

        var rewards = failureRewardsPreview != null ? failureRewardsPreview : new List<ResultViewBase.RewardEntry>();
        int? reached = (reachedPercentOnFailure >= 0) ? (int?)reachedPercentOnFailure : null;

        resultUIController.ShowFailureUIWithSequence(rewards, reached);
    }
    #endregion

#if UNITY_EDITOR
    // Quick test helpers (right-click on component)
    [ContextMenu("Debug/Simulate Success")]
    private void Debug_SimSuccess() => HandleLevelSucceeded();

    [ContextMenu("Debug/Simulate Fail → Dead → Failure")]
    private void Debug_SimFailToFailure()
    {
        HandleLevelFailed();
        // simulate not revive after 1s
        Invoke(nameof(HandleNotReviveChosen), 1f);
    }

    [ContextMenu("Debug/Simulate Fail → Dead → Revive")]
    private void Debug_SimFailToRevive()
    {
        HandleLevelFailed();
        Invoke(nameof(HandleReviveChosen), 1f);
    }
#endif
}
