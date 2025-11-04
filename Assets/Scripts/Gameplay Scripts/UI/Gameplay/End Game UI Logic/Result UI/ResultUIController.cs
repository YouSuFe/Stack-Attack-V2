using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls toggling between "Success" and "Failure" UI groups at the GROUP level.
/// This only handles visibility/interactivity/fade on the two groups.
/// Internal sequencing is handled by ResultSuccessView / ResultFailureView (via ResultViewBase).
/// </summary>
[DisallowMultipleComponent]
public class ResultUIController : MonoBehaviour
{
    #region Constants
    private const float MIN_FADE_DURATION = 0f;
    #endregion

    #region Serialized
    [Header("Group Roots (Scripts)")]
    [SerializeField, Tooltip("Root script for the Success UI group.")]
    private ResultSuccessView successView;

    [SerializeField, Tooltip("Root script for the Failure UI group.")]
    private ResultFailureView failureView;

    [Header("Behavior")]
    [SerializeField, Tooltip("Hide both groups on Awake(). Recommended if this UI is shown contextually.")]
    private bool startHidden = true;

    [Header("Transition")]
    [SerializeField, Tooltip("If true, groups will fade in/out using their CanvasGroup.")]
    private bool useFade = true;

    [SerializeField, Range(0f, 2f), Tooltip("Duration of fade transitions in seconds.")]
    private float fadeDuration = 0.25f;
    #endregion

    #region Private
    private CanvasGroup successCg;
    private CanvasGroup failureCg;
    private Coroutine currentTransition;
    #endregion

    #region Properties
    public bool IsShowingSuccess => successCg != null && successCg.gameObject.activeSelf && successCg.alpha > 0.99f;
    public bool IsShowingFailure => failureCg != null && failureCg.gameObject.activeSelf && failureCg.alpha > 0.99f;
    #endregion

    #region Unity
    private void Awake()
    {
        InitializeComponents();

        if (startHidden)
        {
            InstantHide(successCg);
            InstantHide(failureCg);
        }
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        if (successView == null || failureView == null)
        {
            Debug.LogWarning("[ResultUIController] Please assign SuccessView and FailureView in the Inspector.");
            return;
        }

        successCg = EnsureCanvasGroup(successView.gameObject);
        failureCg = EnsureCanvasGroup(failureView.gameObject);
    }

    private CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        if (target == null) return null;

        if (!target.TryGetComponent(out CanvasGroup cg))
        {
            cg = target.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        return cg;
    }

    private bool ValidateGroups()
    {
        if (successView == null || failureView == null || successCg == null || failureCg == null)
        {
            Debug.LogError("[ResultUIController] Groups are not initialized. Check assignments on the component.");
            return false;
        }
        return true;
    }
    #endregion

    #region Public API - Basic Show/Hide
    /// <summary>Show Success group and hide Failure group (group-level only).</summary>
    public void ShowSuccessUI()
    {
        if (!ValidateGroups()) return;
        SwapGroups(show: successCg, hide: failureCg);
    }

    /// <summary>Show Failure group and hide Success group (group-level only).</summary>
    public void ShowFailureUI()
    {
        if (!ValidateGroups()) return;
        SwapGroups(show: failureCg, hide: successCg);
    }

    /// <summary>Hide both groups.</summary>
    public void HideAll()
    {
        if (!ValidateGroups()) return;
        StartTransition(null, null, hideBoth: true);
    }
    #endregion

    #region Public API - With Sequences
    /// <summary>
    /// Shows Success and immediately plays its internal sequence.
    /// </summary>
    public void ShowSuccessUIWithSequence(IEnumerable<ResultViewBase.RewardEntry> rewards)
    {
        ShowSuccessUI();
        successView?.PlaySequence(rewards);
    }

    /// <summary>
    /// Shows Failure and immediately plays its internal sequence. Pass null to keep existing reached text.
    /// </summary>
    public void ShowFailureUIWithSequence(IEnumerable<ResultViewBase.RewardEntry> rewards, int? reachedPercent = null)
    {
        ShowFailureUI();
        if (failureView != null)
        {
            failureView.ReachedPercent = reachedPercent;
            failureView.PlaySequence(rewards);
        }
    }
    #endregion

    #region Transitions (unscaled time to work during pauses)
    private void SwapGroups(CanvasGroup show, CanvasGroup hide)
    {
        StartTransition(show, hide, hideBoth: false);
    }

    private void StartTransition(CanvasGroup show, CanvasGroup hide, bool hideBoth)
    {
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
            currentTransition = null;
        }

        if (!useFade || fadeDuration <= MIN_FADE_DURATION)
        {
            if (hideBoth)
            {
                InstantHide(successCg);
                InstantHide(failureCg);
            }
            else
            {
                if (hide != null) InstantHide(hide);
                if (show != null) InstantShow(show);
            }
            return;
        }

        currentTransition = StartCoroutine(DoTransition(show, hide, hideBoth));
    }

    private IEnumerator DoTransition(CanvasGroup show, CanvasGroup hide, bool hideBoth)
    {
        if (hideBoth)
        {
            yield return FadeOut(successCg, fadeDuration);
            yield return FadeOut(failureCg, fadeDuration);
            currentTransition = null;
            yield break;
        }

        if (hide != null)
            yield return FadeOut(hide, fadeDuration);

        if (show != null)
            yield return FadeIn(show, fadeDuration);

        currentTransition = null;
    }

    private void InstantShow(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.gameObject.SetActive(true);
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    private void InstantHide(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }

    private IEnumerator FadeIn(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;

        cg.gameObject.SetActive(true);
        cg.blocksRaycasts = true;

        float start = cg.alpha;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, 1f, Mathf.Clamp01(t / duration));
            yield return null;
        }

        cg.alpha = 1f;
        cg.interactable = true;
    }

    private IEnumerator FadeOut(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;

        cg.interactable = false;

        float start = cg.alpha;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / duration));
            yield return null;
        }

        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Show Success UI")]
    private void DebugShowSuccess() => ShowSuccessUIWithSequence(rewards:null);
    [ContextMenu("Debug/Show Failure UI")]
    private void DebugShowFailure() => ShowFailureUIWithSequence(null);
    [ContextMenu("Debug/Hide All")]
    private void DebugHideAll() => HideAll();

    private void OnValidate()
    {
        if (fadeDuration < MIN_FADE_DURATION) fadeDuration = MIN_FADE_DURATION;

        if (successView != null) successCg = EnsureCanvasGroup(successView.gameObject);
        if (failureView != null) failureCg = EnsureCanvasGroup(failureView.gameObject);
    }
#endif
}
