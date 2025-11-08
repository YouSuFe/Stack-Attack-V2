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
    [SerializeField, Tooltip("Runtime reward calculator for this scene.")]
    private RewardRuntime rewardRuntime;

    [SerializeField, Tooltip("Controls Success/Failure result parents (owns enable/disable).")]
    private ResultUIController resultUIController;

    [SerializeField, Tooltip("Dead (KO) canvas with revive / not-revive and countdown.")]
    private DeadCanvasController deadCanvas;

    [Header("Progress Source")]
    [SerializeField, Tooltip("Runtime that exposes ProgressPercent (0–100).")]
    private LevelProgressRuntime progressRuntime;
    #endregion

    #region Optional Data
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

        rewardRuntime?.BuildNow(success: true);

        var entries = rewardRuntime != null
            ? new List<ResultViewBase.RewardEntry>(rewardRuntime.LastEntries)
            : new List<ResultViewBase.RewardEntry>();

        resultUIController.ShowSuccessUIWithSequence(entries);
    }

    private void TriggerFailureUI()
    {
        if (resultUIController == null)
        {
            Debug.LogWarning("[GameUIFlowController] ResultUIController is not assigned.");
            return;
        }

        rewardRuntime?.BuildNow(success: false);

        int reachedPercent = rewardRuntime != null ? rewardRuntime.LastReachedPercent : 0;

        var entries = rewardRuntime != null
            ? new List<ResultViewBase.RewardEntry>(rewardRuntime.LastEntries)
            : new List<ResultViewBase.RewardEntry>();

        resultUIController.ShowFailureUIWithSequence(entries, reachedPercent);
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

#if UNITY_EDITOR
    [Header("Reward Debug (Editor Only)")]
    [SerializeField, Tooltip("When true, debug simulate buttons use this % instead of LevelProgressRuntime.")]
    private bool useDebugReachedPercent = false;

    [SerializeField, Range(0, 100), Tooltip("Reached % used by debug simulate buttons when override is enabled.")]
    private int debugReachedPercent = 75;

    private int? GetDebugOverridePercent() => useDebugReachedPercent ? (int?)debugReachedPercent : null;

    [ContextMenu("Debug/Simulate SUCCESS (Build & Show)")]
    private void DebugSimulateSuccess()
    {
        if (resultUIController == null)
        {
            Debug.LogWarning("[GameUIFlowController] ResultUIController is not assigned.");
            return;
        }

        rewardRuntime?.BuildNow(success: true, overrideReachedPercent: GetDebugOverridePercent());

        var entries = rewardRuntime != null
            ? new List<ResultViewBase.RewardEntry>(rewardRuntime.LastEntries)
            : new List<ResultViewBase.RewardEntry>();

        resultUIController.ShowSuccessUIWithSequence(entries);
        Debug.Log("[GameUIFlowController] Debug Success shown.");
    }

    [ContextMenu("Debug/Simulate FAILURE (Build & Show)")]
    private void DebugSimulateFailure()
    {
        if (resultUIController == null)
        {
            Debug.LogWarning("[GameUIFlowController] ResultUIController is not assigned.");
            return;
        }

        rewardRuntime?.BuildNow(success: false, overrideReachedPercent: GetDebugOverridePercent());

        int reachedPercent = rewardRuntime != null ? rewardRuntime.LastReachedPercent : 0;

        var entries = rewardRuntime != null
            ? new List<ResultViewBase.RewardEntry>(rewardRuntime.LastEntries)
            : new List<ResultViewBase.RewardEntry>();

        resultUIController.ShowFailureUIWithSequence(entries, reachedPercent);
        Debug.Log("[GameUIFlowController] Debug Failure shown.");
    }
#endif
}
