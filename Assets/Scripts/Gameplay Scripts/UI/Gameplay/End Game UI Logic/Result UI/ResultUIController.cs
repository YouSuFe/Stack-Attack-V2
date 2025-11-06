using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates which Result UI group (Success or Failure) is shown.
/// - Both groups are disabled on Awake (optional).
/// - Only one group is ever active at a time.
/// - No fading, no swapping, no coroutines.
/// - Each view (ResultSuccessView / ResultFailureView) handles its own animations.
/// </summary>
[DisallowMultipleComponent]
public class ResultUIController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Group Roots (Scripts)")]
    [SerializeField, Tooltip("Root script for the Success UI group.")]
    private ResultSuccessView successView;

    [SerializeField, Tooltip("Root script for the Failure UI group.")]
    private ResultFailureView failureView;

    [Header("Behavior")]
    [SerializeField, Tooltip("Hide both groups on Awake().")]
    private bool startHidden = true;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (startHidden)
            HideAll();
    }
    #endregion

    #region Public API

    /// <summary>Hides both Success and Failure groups instantly.</summary>
    public void HideAll()
    {
        if (successView != null && successView.gameObject.activeSelf)
            successView.gameObject.SetActive(false);

        if (failureView != null && failureView.gameObject.activeSelf)
            failureView.gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows Success group and triggers its internal animation sequence.
    /// </summary>
    public void ShowSuccessUIWithSequence(IEnumerable<ResultViewBase.RewardEntry> rewards)
    {
        if (successView == null)
        {
            Debug.LogWarning("[ResultUIController] SuccessView is not assigned.");
            return;
        }

        HideAll();
        successView.gameObject.SetActive(true);
        successView.PlaySequence(rewards);
    }

    /// <summary>
    /// Shows Failure group and triggers its internal animation sequence.
    /// </summary>
    public void ShowFailureUIWithSequence(IEnumerable<ResultViewBase.RewardEntry> rewards, int? reachedPercent = null)
    {
        if (failureView == null)
        {
            Debug.LogWarning("[ResultUIController] FailureView is not assigned.");
            return;
        }

        HideAll();
        failureView.gameObject.SetActive(true);
        failureView.ReachedPercent = reachedPercent;
        failureView.PlaySequence(rewards);
    }

    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Show Success (no rewards)")]
    private void DebugShowSuccess() => ShowSuccessUIWithSequence(null);

    [ContextMenu("Debug/Show Failure (no rewards)")]
    private void DebugShowFailure() => ShowFailureUIWithSequence(null);

    [ContextMenu("Debug/Hide All")]
    private void DebugHideAll() => HideAll();
#endif
}
