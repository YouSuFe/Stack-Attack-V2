using PixeLadder.EasyTransition;
using UnityEngine;

/// <summary>
/// LevelService
/// Persistent runtime authority for level selection, progress, and "last played" UI state.
/// - Keeps track of current level, highest unlocked level, and last played index
/// - Persists data in PlayerPrefs
/// - Starts gameplay scenes via SceneTransitioner
/// 
/// UI Rules implemented:
/// - On app start, UI should show "last played" level (lastPlayedIndex)
/// - If player completed a level, lastPlayedIndex becomes NEXT level (if any)
/// - If player failed/quit, lastPlayedIndex stays at CURRENT level
/// 
/// Typical flow:
/// - Main Menu: call GetLastPlayedLevelIndex() to highlight UI
/// - Play: call StartLevel(index) -> this marks "last played" to index
/// - On win: call MarkLevelCompletedAndAdvanceForUI()
/// - On fail/quit: call MarkLevelFailedOrQuitForUI()
/// 
/// Notes:
/// - LevelCatalog contains ordered LevelDefinition assets
/// - You can still SetCurrentLevel(index) directly if you just want to preview data
/// </summary>
[DefaultExecutionOrder(-200)]
public class LevelService : MonoBehaviour
{
    #region PlayerPrefs Keys
    private const string PREF_CURRENT_LEVEL = "LV_Current";
    private const string PREF_HIGHEST_UNLOCKED = "LV_HighestUnlocked";
    private const string PREF_LAST_PLAYED = "LV_LastPlayed";
    #endregion

    #region Singleton
    public static LevelService Instance { get; private set; }
    #endregion

    #region Serialized Fields
    [Header("Data")]
    [SerializeField, Tooltip("Catalog containing all levels in order.")]
    private LevelCatalog levelCatalog;

    [Header("Startup")]
    [SerializeField, Tooltip("Load progress from PlayerPrefs on Awake.")]
    private bool loadProgressOnAwake = true;

    [SerializeField, Tooltip("Default level index used if there is no save.")]
    private int defaultStartLevelIndex = 0;
    #endregion

    #region Runtime State
    [SerializeField, Tooltip("Current level index selected/being played (runtime).")]
    private int currentLevelIndex = -1;

    [SerializeField, Tooltip("Highest unlocked level index (inclusive).")]
    private int highestUnlockedLevelIndex = 0;

    [SerializeField, Tooltip("Index used by UI on app start. Updates on start/complete/fail per rules above.")]
    private int lastPlayedIndex = 0;
    #endregion

    #region Public Properties
    public LevelCatalog Catalog => levelCatalog;
    public int LevelCount => levelCatalog != null ? levelCatalog.Count : 0;

    public int CurrentLevelIndex => currentLevelIndex;
    public int HighestUnlockedLevelIndex => highestUnlockedLevelIndex;
    public int LastPlayedLevelIndex => lastPlayedIndex;

    public LevelDefinition CurrentLevel =>
        (IsValidIndex(currentLevelIndex) ? levelCatalog.Get(currentLevelIndex) : null);

    public LevelDefinition LastPlayedLevel =>
        (IsValidIndex(lastPlayedIndex) ? levelCatalog.Get(lastPlayedIndex) : null);
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton bootstrap
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (levelCatalog == null)
            Debug.LogError("[LevelService] LevelCatalog is not assigned.");

        if (loadProgressOnAwake) LoadProgress();
        else EnsureDefaultProgress();
    }
    #endregion

    #region Public API - Queries
    /// <summary>Returns the index your Main Menu UI should show on app start.</summary>
    public int GetLastPlayedLevelIndex() => lastPlayedIndex;

    /// <summary>Returns the LevelDefinition your Main Menu UI should show on app start.</summary>
    public LevelDefinition GetLastPlayedLevel() => LastPlayedLevel;

    /// <summary>True if the given index is valid and unlocked.</summary>
    public bool IsUnlocked(int index) => IsValidIndex(index) && index <= highestUnlockedLevelIndex;
    #endregion

    #region Public API - Selection / Start
    /// <summary>
    /// Sets the current level index without loading a scene. Does not touch lastPlayed.
    /// Useful for editor previews or menus that want to "select" before play.
    /// </summary>
    public bool SetCurrentLevel(int index)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogWarning($"[LevelService] Invalid level index: {index}");
            return false;
        }
        currentLevelIndex = index;
        SaveCurrentOnly();
        return true;
    }

    /// <summary>
    /// Starts the given level: validates, sets current, sets lastPlayed to this level (so UI shows this if we quit),
    /// and loads the gameplay scene with a random fade effect.
    /// </summary>
    public void StartLevel(int index)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogWarning($"[LevelService] Cannot start invalid level {index}.");
            return;
        }
        if (!IsUnlocked(index))
        {
            Debug.LogWarning($"[LevelService] Level {index} is locked.");
            return;
        }

        currentLevelIndex = index;
        lastPlayedIndex = index;   // If user quits/loses, menu should show this again
        SaveProgress();

        SceneTransitioner.Instance.LoadScene(SceneNames.GamePlay);
    }

    /// <summary>
    /// Restarts the current level (reloads gameplay scene).
    /// Keeps lastPlayedIndex = current (already set on StartLevel).
    /// </summary>
    public void RestartCurrentLevel()
    {
        if (!IsValidIndex(currentLevelIndex))
        {
            Debug.LogWarning("[LevelService] No current level to restart.");
            return;
        }

        SceneTransitioner.Instance.LoadScene(SceneNames.GamePlay);
    }

    /// <summary>
    /// Starts the last played level (used for a "Continue" button on Main Menu).
    /// </summary>
    public void ContinueLastPlayed()
    {
        if (!IsValidIndex(lastPlayedIndex))
        {
            Debug.LogWarning("[LevelService] No valid last played level to continue.");
            return;
        }
        StartLevel(lastPlayedIndex);
    }
    #endregion

    #region Public API - Outcomes (call these from gameplay)
    /// <summary>
    /// Call when the player completes the current level:
    /// - Unlocks the next level (if any)
    /// - Sets lastPlayedIndex to the NEXT level (so the Main Menu UI shows it)
    /// - Persists progress
    /// </summary>
    public void MarkLevelCompletedAndAdvanceForUI()
    {
        if (!IsValidIndex(currentLevelIndex))
            return;

        int next = currentLevelIndex + 1;

        // Unlock next level if it exists
        if (IsValidIndex(next))
            highestUnlockedLevelIndex = Mathf.Max(highestUnlockedLevelIndex, next);

        // For UI on next app start: show next if valid, else stay at current (end of campaign)
        lastPlayedIndex = IsValidIndex(next) ? next : currentLevelIndex;

        SaveProgress();
        Debug.Log($"[LevelService] Level {currentLevelIndex} completed. Next shown in UI: {lastPlayedIndex}. Unlocked up to: {highestUnlockedLevelIndex}");
    }

    /// <summary>
    /// Call when the player fails or quits mid-level:
    /// - Keeps lastPlayedIndex at current level (so the Main Menu UI shows the same level again)
    /// - Persists progress
    /// </summary>
    public void MarkLevelFailedOrQuitForUI()
    {
        if (!IsValidIndex(currentLevelIndex))
            return;

        lastPlayedIndex = currentLevelIndex; // show same level again
        SaveProgress();
        Debug.Log($"[LevelService] Level {currentLevelIndex} failed/quit. UI will show same level again.");
    }
    #endregion

    #region Save / Load
    public void SaveProgress()
    {
        PlayerPrefs.SetInt(PREF_CURRENT_LEVEL, Mathf.Max(0, currentLevelIndex));
        PlayerPrefs.SetInt(PREF_HIGHEST_UNLOCKED, Mathf.Max(0, highestUnlockedLevelIndex));
        PlayerPrefs.SetInt(PREF_LAST_PLAYED, Mathf.Max(0, lastPlayedIndex));
        PlayerPrefs.Save();
    }

    public void SaveCurrentOnly()
    {
        PlayerPrefs.SetInt(PREF_CURRENT_LEVEL, Mathf.Max(0, currentLevelIndex));
        PlayerPrefs.Save();
    }

    public void LoadProgress()
    {
        if (LevelCount <= 0)
        {
            EnsureDefaultProgress();
            return;
        }

        int maxIndex = LevelCount - 1;

        highestUnlockedLevelIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(PREF_HIGHEST_UNLOCKED, defaultStartLevelIndex),
            0, maxIndex);

        currentLevelIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(PREF_CURRENT_LEVEL, defaultStartLevelIndex),
            0, maxIndex);

        lastPlayedIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(PREF_LAST_PLAYED, defaultStartLevelIndex),
            0, maxIndex);

        // Ensure consistency: lastPlayed should never exceed highestUnlocked+1 (optional).
        lastPlayedIndex = Mathf.Clamp(lastPlayedIndex, 0, Mathf.Max(0, highestUnlockedLevelIndex + 1, lastPlayedIndex));
    }

    private void EnsureDefaultProgress()
    {
        int maxIndex = Mathf.Max(0, LevelCount - 1);
        highestUnlockedLevelIndex = Mathf.Clamp(defaultStartLevelIndex, 0, maxIndex);
        currentLevelIndex = Mathf.Clamp(defaultStartLevelIndex, 0, maxIndex);
        lastPlayedIndex = Mathf.Clamp(defaultStartLevelIndex, 0, maxIndex);
        SaveProgress();
    }
    #endregion

    #region Helpers
    private bool IsValidIndex(int index)
    {
        return levelCatalog != null && index >= 0 && index < levelCatalog.Count;
    }
    #endregion

#if UNITY_EDITOR
    #region Editor Utilities
    [ContextMenu("Debug/Reset Progress")]
    private void Debug_ResetProgress()
    {
        PlayerPrefs.DeleteKey(PREF_CURRENT_LEVEL);
        PlayerPrefs.DeleteKey(PREF_HIGHEST_UNLOCKED);
        PlayerPrefs.DeleteKey(PREF_LAST_PLAYED);
        LoadProgress();
        Debug.Log("[LevelService] Progress reset.");
    }

    [ContextMenu("Debug/Unlock All")]
    private void Debug_UnlockAll()
    {
        highestUnlockedLevelIndex = Mathf.Max(0, LevelCount - 1);
        SaveProgress();
        Debug.Log("[LevelService] All levels unlocked.");
    }

    [ContextMenu("Debug/Print State")]
    private void Debug_PrintState()
    {
        Debug.Log($"[LevelService] Current={currentLevelIndex}, HighestUnlocked={highestUnlockedLevelIndex}, LastPlayed={lastPlayedIndex}, Count={LevelCount}");
    }
    #endregion
#endif
}
