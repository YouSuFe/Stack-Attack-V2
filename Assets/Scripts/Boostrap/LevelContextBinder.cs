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
    [Header("Inject Targets (optional auto-find)")]
    [SerializeField, Tooltip("Sequencer that consumes LevelDefinition to run the level.")]
    private LevelSegmentSequencer sequencer;

    [SerializeField, Tooltip("Announcer that previews upcoming segments and needs LevelDefinition.")]
    private UpcomingSegmentAnnouncer announcer;
    #endregion

    #region Outcome Sources (can be hooked dynamically)
    [Header("Outcome Sources (hook at runtime if spawned)")]
    [SerializeField, Tooltip("Player health for fail detection. Can be assigned later via HookPlayer().")]
    private PlayerHealth playerHealth;

    [SerializeField, Tooltip("Boss controller for success detection (pinata ended). HookBoss() when spawned.")]
    private BossStateController bossController;
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

    #region Public Events
    public event Action OnLevelSucceeded;
    public event Action OnLevelFailed;
    #endregion

    #region Public Accessors
    public LevelDefinition CurrentLevelDefinition { get; private set; }
    public bool HasBoundBoss => bossController != null;
    public bool HasBoundPlayer => playerHealth != null;
    #endregion

    #region Private
    private bool outcomeHandled; // guard against double fire
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Scene-local singleton (not persistent)
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LevelContextBinder] Duplicate binder in scene; destroying this instance.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Inject LevelDefinition early so Start() users already have it
        CurrentLevelDefinition = ResolveLevelDefinition();
        if (CurrentLevelDefinition == null)
        {
            Debug.LogError("[LevelContextBinder] Could not resolve LevelDefinition. Ensure LevelService exists and/or enable fallbacks.");
        }
        else
        {
            if (sequencer != null) sequencer.SetLevelDefinition(CurrentLevelDefinition);
            if (announcer != null) announcer.SetLevelDefinition(CurrentLevelDefinition);
            if (verboseLogs) Debug.Log($"[LevelContextBinder] Injected LevelDefinition: {CurrentLevelDefinition.name}");
        }
    }

    private void OnEnable()
    {
        // If player already present, hook now
        HookPlayer(playerHealth);
        // Boss will typically be hooked later by spawner/sequencer via HookBoss()
    }

    private void OnDisable()
    {
        UnhookPlayer(playerHealth);
        UnhookBoss(bossController);
    }
    #endregion

    #region Public Hook API (call these from your sequencer/spawner)
    /// <summary>Hook a PlayerHealth at runtime (safe to call with null).</summary>
    public void HookPlayer(PlayerHealth player)
    {
        if (!player) return;

        // Unhook previous (if any)
        UnhookPlayer(playerHealth);

        playerHealth = player;
        playerHealth.OnDied += HandlePlayerDied;

        if (verboseLogs) Debug.Log("[LevelContextBinder] Player hooked for outcome listening.");
    }

    public void UnhookPlayer(PlayerHealth player)
    {
        if (!player) return;

        player.OnDied -= HandlePlayerDied;

        if (playerHealth == player)
            playerHealth = null;
    }

    /// <summary>Hook a BossStateController at runtime (call right after you instantiate the boss).</summary>
    public void HookBoss(BossStateController boss)
    {
        if (!boss) return;

        // Unhook previous, if any
        UnhookBoss(bossController);

        bossController = boss;
        bossController.OnPinataEnded += HandleBossPinataEnded;

        if (verboseLogs) Debug.Log("[LevelContextBinder] Boss hooked for success listening.");
    }

    public void UnhookBoss(BossStateController boss)
    {
        if (!boss) return;

        boss.OnPinataEnded -= HandleBossPinataEnded;

        if (bossController == boss)
            bossController = null;
    }
    #endregion

    #region Outcome Handlers (update LevelService only; no UI / transitions)
    private void HandlePlayerDied()
    {
        if (outcomeHandled) return;
        outcomeHandled = true;

        if (verboseLogs) Debug.Log("[LevelContextBinder] Outcome: FAIL (player died)");
        LevelService.Instance?.MarkLevelFailedOrQuitForUI();
        OnLevelFailed?.Invoke();
    }

    private void HandleBossPinataEnded()
    {
        if (outcomeHandled) return;
        outcomeHandled = true;

        if (verboseLogs) Debug.Log("[LevelContextBinder] Outcome: SUCCESS (pinata ended)");
        LevelService.Instance?.MarkLevelCompletedAndAdvanceForUI();
        OnLevelSucceeded?.Invoke();
    }
    #endregion

    #region Helpers
    private LevelDefinition ResolveLevelDefinition()
    {
        if (LevelService.Instance == null || LevelService.Instance.Catalog == null)
            return null;

        // Priority: Current → LastPlayed → Index 0
        var def = LevelService.Instance.CurrentLevel;
        if (def == null && fallbackToLastPlayed)
            def = LevelService.Instance.GetLastPlayedLevel();
        if (def == null && fallbackToIndexZero && LevelService.Instance.LevelCount > 0)
            def = LevelService.Instance.Catalog.Get(0);

        return def;
    }

    /// <summary>Optional helper if another script needs the level immediately in Awake/OnEnable.</summary>
    public bool TryGetLevelDefinition(out LevelDefinition def)
    {
        def = CurrentLevelDefinition ?? ResolveLevelDefinition();
        return def != null;
    }
    #endregion
}
