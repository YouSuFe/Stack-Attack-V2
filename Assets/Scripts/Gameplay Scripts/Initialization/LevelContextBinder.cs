using UnityEngine;
using System;

[DefaultExecutionOrder(-10)]
[DisallowMultipleComponent]
public class LevelContextBinder : MonoBehaviour
{
    #region Scene-Local Singleton
    public static LevelContextBinder Instance { get; private set; }
    #endregion

    #region Inject Targets
    [Header("Inject Targets")]
    [SerializeField, Tooltip("Sequencer that consumes LevelDefinition to run the level.")]
    private LevelSegmentSequencer sequencer;

    [SerializeField, Tooltip("Announcer that previews upcoming segments and needs LevelDefinition.")]
    private UpcomingSegmentAnnouncer announcer;

    [SerializeField, Tooltip("Level progress runtime that computes progress from LevelDefinition.")]
    private LevelProgressRuntime progressRuntime;
    #endregion

    #region Resolution Options
    [Header("Level Definition Resolution")]
    [SerializeField, Tooltip("If no current level in LevelService, try last played.")]
    private bool fallbackToLastPlayed = true;

    [SerializeField, Tooltip("If still not found and catalog is not empty, use index 0.")]
    private bool fallbackToIndexZero = true;

    [SerializeField, Tooltip("Verbose logs for setup and outcomes.")]
    private bool verboseLogs = false;
    #endregion

    #region Outcome Sources (optional hooks)
    [Header("Outcome Sources (hook at runtime if spawned)")]
    [SerializeField, Tooltip("Player health for fail detection. Can be assigned later via HookPlayer().")]
    private PlayerHealth playerHealth;

    [SerializeField, Tooltip("Boss controller for success detection (pinata ended). HookBoss() when spawned.")]
    private BossStateController bossController;
    #endregion

    #region Events
    public event Action OnLevelSucceeded;
    public event Action OnLevelFailed;

    /// <summary>
    /// Fired after the binder resolves the level context. Args: (currentLevelIndex, levelDefinition)
    /// </summary>
    public event Action<int, LevelDefinition> OnLevelContextReady;
    #endregion

    #region Public Accessors
    public LevelDefinition CurrentLevelDefinition { get; private set; }
    /// <summary>Zero-based index taken from LevelService (or fallbacks).</summary>
    public int CurrentLevelIndex { get; private set; } = -1;
    /// <summary>1-based convenience accessor.</summary>
    public int CurrentLevelNumber1Based => CurrentLevelIndex + 1;

    public bool HasBoundBoss => bossController != null;
    public bool HasBoundPlayer => playerHealth != null;
    #endregion

    #region Private
    private bool outcomeHandled;
    #endregion

    #region Unity
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LevelContextBinder] Duplicate binder in scene; destroying this instance.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResolveLevelContext();

        if (CurrentLevelDefinition == null)
        {
            Debug.LogError("[LevelContextBinder] Could not resolve LevelDefinition. Ensure LevelService exists and/or enable fallbacks.");
            return;
        }

        // Inject to dependents
        if (sequencer != null) sequencer.SetLevelDefinition(CurrentLevelDefinition);
        if (announcer != null) announcer.SetLevelDefinition(CurrentLevelDefinition);
        if (progressRuntime != null) progressRuntime.SetLevelDefinition(CurrentLevelDefinition);

        if (verboseLogs)
            Debug.Log($"[LevelContextBinder] Injected LevelDefinition: {CurrentLevelDefinition.name} (Index {CurrentLevelIndex})");

        OnLevelContextReady?.Invoke(CurrentLevelIndex, CurrentLevelDefinition);
    }

    private void OnEnable()
    {
        HookPlayer(playerHealth);
    }

    private void OnDisable()
    {
        UnhookPlayer(playerHealth);
        UnhookBoss(bossController);
    }
    #endregion

    #region Public Hook API
    public void HookPlayer(PlayerHealth player)
    {
        if (!player) return;
        UnhookPlayer(playerHealth);
        playerHealth = player;
        playerHealth.OnDied += HandlePlayerDied;
    }

    public void UnhookPlayer(PlayerHealth player)
    {
        if (!player) return;
        player.OnDied -= HandlePlayerDied;
        if (playerHealth == player) playerHealth = null;
    }

    public void HookBoss(BossStateController boss)
    {
        if (!boss) return;
        UnhookBoss(bossController);
        bossController = boss;
        bossController.OnPinataEnded += HandleBossPinataEnded;
    }

    public void UnhookBoss(BossStateController boss)
    {
        if (!boss) return;
        boss.OnPinataEnded -= HandleBossPinataEnded;
        if (bossController == boss) bossController = null;
    }
    #endregion

    #region Outcome Handlers
    private void HandlePlayerDied()
    {
        if (outcomeHandled) return;
        outcomeHandled = true;
        LevelService.Instance?.MarkLevelFailedOrQuitForUI();
        OnLevelFailed?.Invoke();
    }

    private void HandleBossPinataEnded()
    {
        if (outcomeHandled) return;
        outcomeHandled = true;
        LevelService.Instance?.MarkLevelCompletedAndAdvanceForUI();
        OnLevelSucceeded?.Invoke();
    }
    #endregion

    #region Helpers
    private void ResolveLevelContext()
    {
        CurrentLevelDefinition = null;
        CurrentLevelIndex = -1;

        var svc = LevelService.Instance;
        var catalog = svc != null ? svc.Catalog : null;

        if (svc == null || catalog == null)
            return;

        // 1) Prefer service's explicit current level
        if (svc.CurrentLevel != null && svc.CurrentLevelIndex >= 0)
        {
            CurrentLevelDefinition = svc.CurrentLevel;
            CurrentLevelIndex = svc.CurrentLevelIndex;
            return;
        }

        // 2) Fallback to last played (UI)
        if (fallbackToLastPlayed && svc.LastPlayedLevel != null)
        {
            CurrentLevelDefinition = svc.LastPlayedLevel;
            CurrentLevelIndex = svc.LastPlayedLevelIndex;
            return;
        }

        // 3) Fallback to index 0
        if (fallbackToIndexZero && svc.LevelCount > 0)
        {
            CurrentLevelDefinition = catalog.Get(0);
            CurrentLevelIndex = 0;
        }
    }

    /// <summary>Convenience for external callers that need the level immediately.</summary>
    public bool TryGetLevelDefinition(out LevelDefinition def)
    {
        def = CurrentLevelDefinition ?? LevelService.Instance?.CurrentLevel;
        return def != null;
    }
    #endregion
}
