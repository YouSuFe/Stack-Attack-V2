using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Failure result view: defines references, reveal order, and formats the "Reached" text
/// using an int percent passed in from the controller.
/// </summary>
[DisallowMultipleComponent]
public class ResultFailureView : ResultViewBase
{
    #region Serialized - Failure References
    [Header("Failure - References (Top to Bottom)")]
    [SerializeField, Tooltip("Optional background image that appears first.")]
    private Image backgroundImage;

    [SerializeField, Tooltip("Stage text (TMP). Leave null if unused.")]
    private TMP_Text stageText;

    [SerializeField, Tooltip("Lost text (TMP).")]
    private TMP_Text lostText;

    [SerializeField, Tooltip("Icon image under the Lost text.")]
    private Image iconImage;

    [SerializeField, Tooltip("Reached text (TMP).")]
    private TMP_Text reachedText;

    [SerializeField, Tooltip("Collect Rewards text (TMP).")]
    private TMP_Text collectRewardsText;

    [SerializeField, Tooltip("Double Rewards button (optional).")]
    private Button doubleRewardsButton;

    [SerializeField, Tooltip("Continue text or container.")]
    private GameObject continueContainer;

    [Header("Reached Text Formatting")]
    [SerializeField, Tooltip("If true and a value is provided, formats reachedText as \"REACHED {X}%\".")]
    private bool formatReachedAsPercent = true;

    [SerializeField, Tooltip("Uppercase label shown before value when formatting reachedText.")]
    private string reachedPrefix = "REACHED";
    #endregion

    #region Input From Controller
    /// <summary>Set by controller before PlaySequence. If null, keeps existing text.</summary>
    public int? ReachedPercent { get; set; }
    #endregion

    #region Cached CGs
    private CanvasGroup bgCg, stageCg, lostCg, iconCg, reachedCg, collectRewardsCg, gridCg, doubleBtnCg, continueCg;
    #endregion

    #region Base Overrides
    protected override void EnsureAllCanvasGroups()
    {
        bgCg = EnsureCg(backgroundImage ? backgroundImage.gameObject : null);
        stageCg = EnsureCg(stageText ? stageText.gameObject : null);
        lostCg = EnsureCg(lostText ? lostText.gameObject : null);
        iconCg = EnsureCg(iconImage ? iconImage.gameObject : null);
        reachedCg = EnsureCg(reachedText ? reachedText.gameObject : null);
        collectRewardsCg = EnsureCg(collectRewardsText ? collectRewardsText.gameObject : null);
        gridCg = EnsureCg(rewardsGridRoot ? rewardsGridRoot.gameObject : null);
        doubleBtnCg = EnsureCg(doubleRewardsButton ? doubleRewardsButton.gameObject : null);
        continueCg = EnsureCg(continueContainer ? continueContainer.gameObject : null);
    }

    protected override CanvasGroup GetBackgroundCanvasGroup() => bgCg;

    protected override CanvasGroup[] BuildSequenceGroups()
    {
        return new[]
        {
            stageCg,
            lostCg,
            iconCg,
            reachedCg,
            collectRewardsCg,
            gridCg,
            doubleBtnCg,
            continueCg
        };
    }

    protected override void OnBeforeSequence()
    {
        if (!formatReachedAsPercent || reachedText == null) return;
        if (ReachedPercent.HasValue)
        {
            int pct = Mathf.Clamp(ReachedPercent.Value, 0, 999);
            reachedText.text = $"{reachedPrefix} {pct}%";
        }
    }
    #endregion
}
