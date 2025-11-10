// MainMenuUIController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main Menu controller (mobile):
/// - Auto-selects last played level on first open (per LevelService).
/// - Shows only unlocked levels; arrows never reveal future locked stages.
/// - Renders Stage N, rank sprite/text (fixed 5-level bands), progress bar, coin text.
/// - Battle button starts the currently selected level (only if unlocked).
/// </summary>
public class MainMenuUIController : MonoBehaviour
{
    #region Serialized UI
    [SerializeField, Tooltip("Top-left coin amount text (TMP).")]
    private TMP_Text coinText;

    [SerializeField, Tooltip("Center 'Stage N' text over your static icon.")]
    private TMP_Text stageText;

    [SerializeField, Tooltip("Rank name text displayed on/over the rank sprite (e.g., 'Bronze II').")]
    private TMP_Text rankText;

    [SerializeField, Tooltip("Rank major sprite (Bronze/Silver/Gold).")]
    private Image rankIcon;

    [SerializeField, Tooltip("Bronze/Silver/Gold sprites.")]
    private RankSpriteSet rankSprites;

    [SerializeField, Tooltip("Progress fill image (0..1). Use Image with Filled type.")]
    private Image progressFillImage;

    [SerializeField, Tooltip("Left arrow button to move selection backward (only within unlocked range).")]
    private Button leftArrowButton;

    [SerializeField, Tooltip("Right arrow button to move selection forward (only within unlocked range).")]
    private Button rightArrowButton;

    [SerializeField, Tooltip("Battle button to start the selected level.")]
    private Button battleButton;
    #endregion

    #region Selection Behavior
    [SerializeField, Tooltip("If true, menu focuses the last played level the first time it opens.")]
    private bool selectLastPlayedOnEnable = true;

    private bool hasInitializedSelection;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        if (TryGetWallet(out var wallet))
            wallet.OnCoinsChanged += HandleCoinsChanged;

        if (LevelProgressServiceExists() && LevelProgressService.Instance != null)
            LevelProgressService.Instance.OnProgressChanged += HandleProgressChanged;

        if (leftArrowButton != null) leftArrowButton.onClick.AddListener(OnClickPrev);
        if (rightArrowButton != null) rightArrowButton.onClick.AddListener(OnClickNext);
        if (battleButton != null) battleButton.onClick.AddListener(OnClickBattle);

        InitializeSelectionIfNeeded();
        RefreshUI();
    }

    private void OnDisable()
    {
        if (TryGetWallet(out var wallet))
            wallet.OnCoinsChanged -= HandleCoinsChanged;

        if (LevelProgressServiceExists() && LevelProgressService.Instance != null)
            LevelProgressService.Instance.OnProgressChanged -= HandleProgressChanged;

        if (leftArrowButton != null) leftArrowButton.onClick.RemoveListener(OnClickPrev);
        if (rightArrowButton != null) rightArrowButton.onClick.RemoveListener(OnClickNext);
        if (battleButton != null) battleButton.onClick.RemoveListener(OnClickBattle);
    }
    #endregion

    #region Initialization
    private void InitializeSelectionIfNeeded()
    {
        if (!selectLastPlayedOnEnable || hasInitializedSelection) return;

        var ls = LevelService.Instance;
        if (ls == null || ls.LevelCount == 0) return;

        // Focus the last played (per your service rules: next on win, same on fail)
        int idx = Mathf.Clamp(ls.GetLastPlayedLevelIndex(), 0, Mathf.Max(0, ls.LevelCount - 1));

        // Also clamp to unlocked range to be extra safe
        idx = Mathf.Min(idx, ls.HighestUnlockedLevelIndex);

        ls.SetCurrentLevel(idx);
        hasInitializedSelection = true;
    }
    #endregion

    #region UI Events
    private void OnClickPrev()
    {
        var ls = LevelService.Instance;
        if (ls == null || ls.LevelCount == 0) return;

        int current = ls.CurrentLevelIndex;
        int prev = current - 1;

        // Only allow browsing within unlocked range [0 .. HighestUnlocked]
        if (prev >= 0 && prev <= ls.HighestUnlockedLevelIndex)
        {
            ls.SetCurrentLevel(prev);
            RefreshUI();
        }
    }

    private void OnClickNext()
    {
        var ls = LevelService.Instance;
        if (ls == null || ls.LevelCount == 0) return;

        int current = ls.CurrentLevelIndex;
        int next = current + 1;

        // Only allow browsing within unlocked range [0 .. HighestUnlocked]
        if (next >= 0 && next <= ls.HighestUnlockedLevelIndex)
        {
            ls.SetCurrentLevel(next);
            RefreshUI();
        }
    }

    private void OnClickBattle()
    {
        var ls = LevelService.Instance;
        if (ls == null || ls.LevelCount == 0) return;

        int index = ls.CurrentLevelIndex;
        if (!ls.IsUnlocked(index))
        {
            Debug.Log($"Level {index} is locked.");
            return;
        }
        ls.StartLevel(index);
    }

    private void HandleCoinsChanged(int newAmount)
    {
        if (coinText != null) coinText.text = newAmount.ToString();
    }

    private void HandleProgressChanged(int levelIndex, float bestProgress)
    {
        var ls = LevelService.Instance;
        if (ls == null) return;
        if (levelIndex != ls.CurrentLevelIndex) return;
        RefreshProgressBar(levelIndex);
    }
    #endregion

    #region UI Refresh
    private void RefreshUI()
    {
        var ls = LevelService.Instance;

        // Coins (optional Wallet)
        if (coinText != null)
        {
            if (TryGetWallet(out var wallet))
                coinText.text = wallet.Coins.ToString();
            else
                coinText.text = "0";
        }

        // No levels case
        if (ls == null || ls.LevelCount == 0)
        {
            if (stageText) stageText.text = "No Levels";
            if (rankText) rankText.text = "";
            if (rankIcon) rankIcon.enabled = false;
            SetArrowsVisible(false, false);
            if (battleButton) battleButton.interactable = false;
            if (progressFillImage) progressFillImage.fillAmount = 0f;
            return;
        }

        // Clamp current selection into unlocked range to avoid showing locked info
        int current = Mathf.Clamp(ls.CurrentLevelIndex, 0, Mathf.Max(0, ls.HighestUnlockedLevelIndex));
        if (current != ls.CurrentLevelIndex)
            ls.SetCurrentLevel(current);

        int stageNumber = current + 1;

        // Stage text
        if (stageText) stageText.text = $"Stage {stageNumber}";

        // Rank (fixed 5-level bands)
        RankStage subrank = RankUtility.GetRankStageForStageNumber(stageNumber);
        RankMajor major = RankUtility.GetMajor(subrank);

        if (rankText != null)
            rankText.text = RankUtility.Format(subrank);

        if (rankIcon != null)
        {
            var sprite = rankSprites != null ? rankSprites.GetSprite(major) : null;
            rankIcon.enabled = sprite != null;
            rankIcon.sprite = sprite;
        }

        // Arrows are visible ONLY within unlocked range
        bool hasPrev = current - 1 >= 0;                                     // there exists a previous (always unlocked by progression)
        bool hasNext = current + 1 <= ls.HighestUnlockedLevelIndex;          // next is unlocked; do not preview locked levels
        SetArrowsVisible(hasPrev, hasNext);

        // Battle interactable only if unlocked (it is, by construction)
        if (battleButton) battleButton.interactable = ls.IsUnlocked(current);

        // Progress
        RefreshProgressBar(current);
    }

    private void RefreshProgressBar(int levelIndex)
    {
        if (progressFillImage == null) return;

        float prog = 0f;
        if (LevelProgressServiceExists() && LevelProgressService.Instance != null)
            prog = Mathf.Clamp01(LevelProgressService.Instance.GetBestProgress(levelIndex));

        progressFillImage.fillAmount = prog;
    }

    private void SetArrowsVisible(bool left, bool right)
    {
        if (leftArrowButton) leftArrowButton.gameObject.SetActive(left);
        if (rightArrowButton) rightArrowButton.gameObject.SetActive(right);
    }
    #endregion

    #region Helpers
    private bool TryGetWallet(out WalletService wallet)
    {
        wallet = WalletServiceExists() ? WalletService.Instance : null;
        return wallet != null;
    }

    private static bool WalletServiceExists()
    {
        return typeof(WalletService) != null && WalletService.Instance != null;
    }

    private static bool LevelProgressServiceExists()
    {
        return typeof(LevelProgressService) != null;
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug: Refresh UI")]
    private void DebugRefresh() => RefreshUI();
#endif
}
