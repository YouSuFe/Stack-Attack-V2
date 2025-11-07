using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Subscribes to level outcome events and triggers the appropriate Result UI:
/// - On success ⇒ calls ResultUIController.ShowSuccessUIWithSequence(...)
/// - On fail ⇒ shows DeadCanvas; if player declines/timeout ⇒ calls ResultUIController.ShowFailureUIWithSequence(...)
/// This class DOES NOT hide/show result groups directly; the ResultUIController handles that.
/// </summary>
[DisallowMultipleComponent]
public class GameUIFlowController : MonoBehaviour
{
    #region Serialized References
    [Header("Controllers")]
    [SerializeField, Tooltip("Controls Success/Failure result parents (owns enable/disable).")]
    private ResultUIController resultUIController;

    [SerializeField, Tooltip("Dead (KO) canvas with revive / not-revive and countdown.")]
    private DeadCanvasController deadCanvas;

    [Header("Progress Source")]
    [SerializeField, Tooltip("Runtime that exposes ProgressPercent (0–100).")]
    private LevelProgressRuntime progressRuntime;
    #endregion

    #region Optional Data (for quick testing)
    [Header("Optional Rewards (Inspector Testing)")]
    [SerializeField, Tooltip("Rewards to display on Success (optional).")]
    private List<ResultViewBase.RewardEntry> successRewardsPreview = new List<ResultViewBase.RewardEntry>();

    [SerializeField, Tooltip("Rewards to display on Failure (optional).")]
    private List<ResultViewBase.RewardEntry> failureRewardsPreview = new List<ResultViewBase.RewardEntry>();

    [SerializeField, Tooltip("If set >=0, Failure UI will show REACHED {value}%. Leave -1 to keep the view's current text.")]
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

    private void Awake()
    {
        // Ensure dead canvas starts hidden every session.
        if (deadCanvas != null)
            deadCanvas.Hide();
    }

    private void OnEnable()
    {
        // Subscribe to level outcome
        binder = LevelContextBinder.Instance;
        if (binder != null)
        {
            binder.OnLevelSucceeded += HandleLevelSucceeded;
            binder.OnLevelFailed += HandleLevelFailed;
        }

        // Subscribe to DeadCanvas choices
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

    #region Public API (optional setters for runtime rewards)
    public void SetSuccessRewards(IEnumerable<ResultViewBase.RewardEntry> rewards)
    {
        successRewardsPreview = rewards != null ? new List<ResultViewBase.RewardEntry>(rewards) : new List<ResultViewBase.RewardEntry>();
    }

    public void SetFailureRewards(IEnumerable<ResultViewBase.RewardEntry> rewards)
    {
        failureRewardsPreview = rewards != null ? new List<ResultViewBase.RewardEntry>(rewards) : new List<ResultViewBase.RewardEntry>();
    }
    #endregion

    #region Level Outcomes
    private void HandleLevelSucceeded()
    {
        Debug.Log("[GameUIFlowController] Outcome: SUCCESS → trigger Success UI");
        HideDeadCanvasIfNeeded();
        TriggerSuccessUI();
    }

    private void HandleLevelFailed()
    {
        Debug.Log("[GameUIFlowController] Outcome: FAIL → show Dead Canvas");
        ShowDeadCanvas();
    }
    #endregion

    #region Dead Canvas Reactions
    private void HandleReviveChosen()
    {
        // Gameplay resumes; no result UI shown here.
        Debug.Log("[GameUIFlowController] DeadCanvas: Revive chosen → resume gameplay.");
        HideDeadCanvasIfNeeded();
    }

    private void HandleNotReviveChosen()
    {
        Debug.Log("[GameUIFlowController] DeadCanvas: Not Revive / timeout → trigger Failure UI");
        HideDeadCanvasIfNeeded();
        TriggerFailureUI();
    }
    #endregion

    #region UI Triggers (no direct show/hide of result groups here)
    private void TriggerSuccessUI()
    {
        if (resultUIController == null)
        {
            Debug.LogWarning("[GameUIFlowController] ResultUIController is not assigned.");
            return;
        }

        var rewards = successRewardsPreview ?? new List<ResultViewBase.RewardEntry>();
        resultUIController.ShowSuccessUIWithSequence(rewards);
    }

    private void TriggerFailureUI()
    {
        if (resultUIController == null)
        {
            Debug.LogWarning("[GameUIFlowController] ResultUIController is not assigned.");
            return;
        }

        var rewards = failureRewardsPreview ?? new List<ResultViewBase.RewardEntry>();
        int? reachedPercent = (progressRuntime != null) ? progressRuntime.ProgressPercent : (int?)null;
        resultUIController.ShowFailureUIWithSequence(rewards, reachedPercent);
    }
    #endregion

    #region Dead Canvas Helpers
    private void ShowDeadCanvas()
    {
        if (deadCanvas == null)
        {
            Debug.LogWarning("[GameUIFlowController] DeadCanvasController is not assigned.");
            return;
        }

        showingDead = true;

        if (deadCountdownSecondsOverride > 0)
            deadCanvas.Show(deadCountdownSecondsOverride);
        else
            deadCanvas.Show();
    }

    private void HideDeadCanvasIfNeeded()
    {
        if (!showingDead || deadCanvas == null) return;
        showingDead = false;
        deadCanvas.Hide();
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Simulate Success")]
    private void Debug_SimSuccess() => HandleLevelSucceeded();

    [ContextMenu("Debug/Simulate Fail → Dead → Failure")]
    private void Debug_SimFailToFailure()
    {
        HandleLevelFailed();
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
