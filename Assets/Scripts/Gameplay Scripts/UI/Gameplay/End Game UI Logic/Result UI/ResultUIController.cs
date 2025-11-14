using System.Collections.Generic;
using UnityEngine;
using PixeLadder.EasyTransition;

[DisallowMultipleComponent]
public class ResultUIController : MonoBehaviour
{
    #region Serialized
    [Header("Group Roots (Scripts)")]
    [SerializeField] private ResultSuccessView successView;
    [SerializeField] private ResultFailureView failureView;

    [Header("Behavior")]
    [SerializeField, Tooltip("Hide both groups on Awake().")]
    private bool startHidden = true;
    #endregion

    #region Runtime Cache
    private List<ResultViewBase.RewardEntry> cachedRewards;
    private int? cachedReachedPercent; // only used on failure
    private bool showingSuccess;
    private bool hasCommitBeenProcessed;
    #endregion

    #region Unity
    private void Awake()
    {
        if (startHidden) HideAll();
    }

    private void OnEnable()
    {
        // Subscribe to button clicks directly
        if (successView != null)
        {
            if (successView.ContinueButton != null)
                successView.ContinueButton.onClick.AddListener(OnCollectClickedSuccess);
            if (successView.DoubleButton != null)
                successView.DoubleButton.onClick.AddListener(OnDoubleClickedSuccess);
        }

        if (failureView != null)
        {
            if (failureView.ContinueButton != null)
                failureView.ContinueButton.onClick.AddListener(OnCollectClickedFailure);
            if (failureView.DoubleButton != null)
                failureView.DoubleButton.onClick.AddListener(OnDoubleClickedFailure);
        }
    }

    private void OnDisable()
    {
        if (successView != null)
        {
            if (successView.ContinueButton != null)
                successView.ContinueButton.onClick.RemoveListener(OnCollectClickedSuccess);
            if (successView.DoubleButton != null)
                successView.DoubleButton.onClick.RemoveListener(OnDoubleClickedSuccess);
        }

        if (failureView != null)
        {
            if (failureView.ContinueButton != null)
                failureView.ContinueButton.onClick.RemoveListener(OnCollectClickedFailure);
            if (failureView.DoubleButton != null)
                failureView.DoubleButton.onClick.RemoveListener(OnDoubleClickedFailure);
        }
    }
    #endregion

    #region Public API
    public void HideAll()
    {
        if (successView != null && successView.gameObject.activeSelf)
            successView.gameObject.SetActive(false);
        if (failureView != null && failureView.gameObject.activeSelf)
            failureView.gameObject.SetActive(false);
    }

    public void ShowSuccessUIWithSequence(IEnumerable<ResultViewBase.RewardEntry> rewards)
    {
        HideAll();
        showingSuccess = true;
        hasCommitBeenProcessed = false;

        if (successView == null) { Debug.LogWarning("[ResultUIController] SuccessView is not assigned."); return; }

        CacheRewards(rewards, null);
        successView.gameObject.SetActive(true);
        successView.PlaySequence(rewards);
    }

    public void ShowFailureUIWithSequence(IEnumerable<ResultViewBase.RewardEntry> rewards, int? reachedPercent = null)
    {
        HideAll();
        showingSuccess = false;
        hasCommitBeenProcessed = false;

        if (failureView == null) { Debug.LogWarning("[ResultUIController] FailureView is not assigned."); return; }

        CacheRewards(rewards, reachedPercent);
        failureView.gameObject.SetActive(true);
        failureView.ReachedPercent = reachedPercent;
        failureView.PlaySequence(rewards);
    }
    #endregion

    #region Button handlers
    private void OnCollectClickedSuccess() => CommitAndExit(rewardMultiplier: 1, isLevelSuccessful: true);
    private void OnDoubleClickedSuccess() => CommitAndExit(rewardMultiplier: 2, isLevelSuccessful: true);
    private void OnCollectClickedFailure() => CommitAndExit(rewardMultiplier: 1, isLevelSuccessful: false);
    private void OnDoubleClickedFailure() => CommitAndExit(rewardMultiplier: 2, isLevelSuccessful: false);
    #endregion

    #region Core
    private void CacheRewards(IEnumerable<ResultViewBase.RewardEntry> rewards, int? reachedPercent)
    {
        cachedRewards = rewards != null
            ? new List<ResultViewBase.RewardEntry>(rewards)
            : new List<ResultViewBase.RewardEntry>();
        cachedReachedPercent = reachedPercent;
    }

    /// <summary>
    /// Commits rewards and progress to the game services, then transitions back to the menu.
    /// Handles both success and failure outcomes.
    /// </summary>
    private void CommitAndExit(int rewardMultiplier, bool isLevelSuccessful)
    {
        // Prevent multiple commits (safety check)
        if (hasCommitBeenProcessed)
            return;

        hasCommitBeenProcessed = true;

        if (showingSuccess && successView != null)
        {
            if (successView.ContinueButton) successView.ContinueButton.interactable = false;
            if (successView.DoubleButton) successView.DoubleButton.interactable = false;
        }
        else if (failureView != null)
        {
            if (failureView.ContinueButton) failureView.ContinueButton.interactable = false;
            if (failureView.DoubleButton) failureView.DoubleButton.interactable = false;
        }

        // --- Get service instances ---
        LevelService levelService = LevelService.Instance;
        WalletService walletService = WalletService.Instance;
        ExpService experienceService = ExpService.Instance;
        LevelProgressService levelProgressService = LevelProgressService.Instance;

        if (levelService == null)
        {
            Debug.LogWarning("[ResultUIController] LevelService is missing. Unable to commit result data.");
            return;
        }

        // --- Calculate total rewards (coins + experience) ---
        int totalCoinsEarned = 0;
        int totalExperienceEarned = 0;

        if (cachedRewards != null)
        {
            foreach (var reward in cachedRewards)
            {
                switch (reward.rewardType)
                {
                    case RewardType.Coin:
                        totalCoinsEarned += Mathf.Max(0, reward.amount);
                        break;

                    case RewardType.Exp:
                        totalExperienceEarned += Mathf.Max(0, reward.amount);
                        break;

                    default:
                        Debug.LogWarning($"[ResultUIController] Unknown reward type: {reward.rewardType}");
                        break;
                }
            }
        }

        // Apply any multiplier (e.g., "Double Reward" button)
        totalCoinsEarned *= Mathf.Max(1, rewardMultiplier);
        totalExperienceEarned *= Mathf.Max(1, rewardMultiplier);

        // --- 1. Update best progress for this level ---
        if (levelProgressService != null)
        {
            float progressNormalized = isLevelSuccessful
                ? 1f
                : Mathf.Clamp01((cachedReachedPercent ?? 0) / 100f);

            levelProgressService.ReportProgress(levelService.CurrentLevelIndex, progressNormalized);
        }

        // --- 2. Apply wallet and experience changes ---
        if (walletService != null && totalCoinsEarned > 0)
        {
            walletService.AddCoins(totalCoinsEarned);
        }

        if (experienceService != null && totalExperienceEarned > 0)
        {
            experienceService.AddExp(totalExperienceEarned, out bool leveledUp, out int levelsGained);

            if (leveledUp)
            {
                Debug.Log($"[ResultUIController] Player leveled up! +{levelsGained} level(s).");
            }
        }

        // --- 3. Update level service state based on outcome ---
        if (isLevelSuccessful)
        {
            levelService.MarkLevelCompletedAndAdvanceForUI();
        }
        else
        {
            levelService.MarkLevelFailedOrQuitForUI();
        }

        // --- 4. Transition back to menu ---
        SceneTransitioner.Instance.LoadScene(SceneNames.MainMenu);
    }

    #endregion
}
