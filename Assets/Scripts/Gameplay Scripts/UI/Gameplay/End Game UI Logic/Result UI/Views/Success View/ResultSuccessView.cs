using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Success result view: defines references and reveal order.
/// All sequencing/animations handled by ResultViewBase.
/// </summary>
[DisallowMultipleComponent]
public class ResultSuccessView : ResultViewBase
{
    #region Serialized - Success References
    [Header("Success - References (Top to Bottom)")]
    [SerializeField, Tooltip("Background image that appears first.")]
    private Image backgroundImage;

    [SerializeField, Tooltip("Stage text (TMP). Leave null if unused.")]
    private TMP_Text stageText;

    [SerializeField, Tooltip("Completed text (TMP).")]
    private TMP_Text completedText;

    [SerializeField, Tooltip("Collect Rewards text (TMP).")]
    private TMP_Text collectRewardsText;

    [SerializeField, Tooltip("Double Rewards button (optional).")]
    private Button doubleRewardsButton;

    [SerializeField, Tooltip("Continue/Collect button.")]
    private Button continueButton;

    #endregion

    public Button ContinueButton => continueButton;
    public Button DoubleButton => doubleRewardsButton;

    #region Cached CGs
    private CanvasGroup bgCg, stageCg, completedCg, collectRewardsCg, gridCg, doubleBtnCg, continueBtnCg;
    #endregion

    #region Base Overrides
    protected override void EnsureAllCanvasGroups()
    {
        bgCg = EnsureCg(backgroundImage ? backgroundImage.gameObject : null);
        stageCg = EnsureCg(stageText ? stageText.gameObject : null);
        completedCg = EnsureCg(completedText ? completedText.gameObject : null);
        collectRewardsCg = EnsureCg(collectRewardsText ? collectRewardsText.gameObject : null);
        gridCg = EnsureCg(rewardsGridRoot ? rewardsGridRoot.gameObject : null);
        doubleBtnCg = EnsureCg(doubleRewardsButton ? doubleRewardsButton.gameObject : null);
        continueBtnCg = EnsureCg(continueButton ? continueButton.gameObject : null);
    }

    protected override CanvasGroup GetBackgroundCanvasGroup() => bgCg;

    protected override CanvasGroup[] BuildSequenceGroups()
    {
        return new[]
        {
            stageCg,
            completedCg,
            collectRewardsCg,
            gridCg,
            doubleBtnCg,
            continueBtnCg
        };
    }
    #endregion
}
