using UnityEngine;

[DisallowMultipleComponent]
public class PinataDirector : MonoBehaviour
{
    #region Singleton
    private static PinataDirector instance;
    public static PinataDirector Instance
    {
        get
        {
            if (instance == null)
                Debug.LogError("[PinataDirector] No instance in scene.");
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[PinataDirector] Multiple instances detected. Destroying extra.");
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (!targetCamera) targetCamera = Camera.main;

        if (pinataBackdrop) pinataBackdrop.SetActive(false);
        if (damageReceiver) damageReceiver.gameObject.SetActive(false);

        // Hide UI at boot (we’ll enable on BeginPinata)
        if (uiController) uiController.gameObject.SetActive(false);

        // Ensure scene meter starts disabled
        if (sceneMeter) sceneMeter.EnablePinata(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
    #endregion

    #region Serialized
    [Header("Positioning")]
    [SerializeField, Tooltip("If assigned, used as exact pinata anchor.")]
    private Transform pinataAnchor;

    [SerializeField, Tooltip("Used if anchor is null. Y = camera top + this offset.")]
    private float cameraTopYOffset = 0.35f;

    [SerializeField] private Camera targetCamera;

    [Header("Coin Multiplier")]
    [SerializeField, Min(1), Tooltip("Coin Multiplier for each reward.")]
    private float coinMultiplier = 5;

    [Header("Presentation")]
    [SerializeField, Tooltip("Backdrop/plate shown during pinata.")]
    private GameObject pinataBackdrop;

    [SerializeField, Tooltip("Damage relay for top hits → PinataMeter.")]
    private PinataDamageReceiver damageReceiver;

    [Header("Pinata Session (Scene-Level)")]
    [SerializeField, Tooltip("Scene-level pinata meter used for sessions.")]
    private PinataMeter sceneMeter;

    [SerializeField, Tooltip("UI controller that drives bar + flags.")]
    private PinataMeterUIController uiController;

    [Header("General UI To Toggle During Boss/Pinata")]
    [SerializeField, Tooltip("Root GameObject for the EXP bar UI (parent object to enable/disable).")]
    private GameObject expBarRoot;

    [SerializeField, Tooltip("Root GameObject for the Level Progress UI (parent object to enable/disable).")]
    private GameObject levelProgressRoot;
    #endregion

    #region Runtime
    private BossStateController activeBoss;
    private PinataMeter activeMeter;
    private bool isRunning;
    #endregion

    #region Public API
    /// <summary>
    /// Preferred entry: uses the scene-level meter & UI.
    /// </summary>
    public void BeginPinataFor(BossStateController boss)
    {
        BeginPinataInternal(boss, sceneMeter);
    }

    public void EndPinata()
    {
        if (!isRunning) return;
        isRunning = false;

        // Hide UI & backdrop
        if (pinataBackdrop) pinataBackdrop.SetActive(false);
        if (uiController) uiController.gameObject.SetActive(false);

        // Stop damage & unsubscribe
        if (damageReceiver) damageReceiver.gameObject.SetActive(false);

        if (activeMeter != null)
        {
            activeMeter.OnRewardsGranted -= HandleRewardsGranted;
            activeMeter.EnablePinata(false);
        }

        activeBoss = null;
        activeMeter = null;
    }
    #endregion

    #region Internal
    private void BeginPinataInternal(BossStateController boss, PinataMeter meterToUse)
    {
        if (!boss)
        {
            Debug.LogWarning("[PinataDirector] BeginPinataFor called with null boss.");
            return;
        }
        if (!meterToUse)
        {
            Debug.LogError("[PinataDirector] No PinataMeter available (assign sceneMeter or pass a meter).");
            return;
        }

        DisableHUDForPinata();

        activeBoss = boss;
        activeMeter = meterToUse;

        // Position boss for pinata presentation
        Vector3 pos = ComputePinataPosition(boss.transform.position);
        boss.transform.position = pos;

        // Show backdrop
        if (pinataBackdrop) pinataBackdrop.SetActive(true);

        // Start meter and hook rewards
        activeMeter.EnablePinata(true);
        activeMeter.OnRewardsGranted += HandleRewardsGranted;

        // Bind receiver so projectiles can damage the pinata
        if (damageReceiver)
        {
            damageReceiver.BindPinataMeter(activeMeter);
            damageReceiver.gameObject.SetActive(true);
        }

        // Show & bind UI
        if (uiController)
        {
            uiController.gameObject.SetActive(true);
            uiController.SetMeter(activeMeter); // runtime bind
        }

        isRunning = true;
    }

    private void DisableHUDForPinata()
    {
        expBarRoot.SetActive(false);
        levelProgressRoot.SetActive(false);
    }

    private void HandleRewardsGranted(int count)
    {
        if (count <= 0) return;

        int coins = (int)(count * coinMultiplier);
        if (CoinSystem.Instance != null)
        {
            CoinSystem.Instance.AddCoins(coins);
        }
        else
        {
            Debug.LogWarning($"[PinataDirector] CoinSystem.Instance is null. Intended to add {coins} coins.");
        }
    }
    #endregion

    #region Helpers
    private Vector3 ComputePinataPosition(Vector3 fallbackAroundX)
    {
        if (pinataAnchor) return pinataAnchor.position;

        var cam = targetCamera ? targetCamera : Camera.main;
        if (!cam) return fallbackAroundX;

        float topY = cam.orthographic
            ? cam.transform.position.y + cam.orthographicSize
            : cam.ViewportToWorldPoint(new Vector3(0f, 1f, Mathf.Abs(cam.transform.position.z))).y;

        float x = cam.transform.position.x; // center horizontally
        return new Vector3(x, topY + cameraTopYOffset, fallbackAroundX.z);
    }
    #endregion

#if UNITY_EDITOR
    #region Debug
    // ===========================
    // DEBUG HELPERS (Editor only)
    // You can remove this whole region later.
    // ===========================

    /// <summary>Start a pinata session WITHOUT a boss, for UI testing.</summary>
    [ContextMenu("Debug/Begin Pinata (No Boss)")]
    private void Debug_BeginPinata_NoBoss()
    {
        if (isRunning) return;

        if (!sceneMeter || !damageReceiver)
        {
            Debug.LogError("[PinataDirector][Debug] Missing references: sceneMeter or damageReceiver.");
            return;
        }

        // Position backdrop & receiver at anchor/auto position (optional visual aid)
        if (pinataBackdrop)
        {
            if (pinataAnchor) pinataBackdrop.transform.position = pinataAnchor.position;
            else pinataBackdrop.transform.position = ComputePinataPosition(pinataBackdrop.transform.position);
            pinataBackdrop.SetActive(true);
        }

        // Use the scene meter for debug session
        activeBoss = null;
        activeMeter = sceneMeter;

        // Enable meter & subscribe rewards
        activeMeter.EnablePinata(true);
        activeMeter.OnRewardsGranted += HandleRewardsGranted;

        // Enable receiver and bind meter (so real projectiles can still hit if you want)
        damageReceiver.BindPinataMeter(activeMeter);
        damageReceiver.gameObject.SetActive(true);

        // Show & bind UI
        if (uiController)
        {
            uiController.gameObject.SetActive(true);
            uiController.SetMeter(activeMeter);
        }

        isRunning = true;
        Debug.Log("[PinataDirector][Debug] Pinata session started (no boss).");
    }

    /// <summary>End the current pinata session.</summary>
    [ContextMenu("Debug/End Pinata")]
    private void Debug_EndPinata()
    {
        EndPinata();
        Debug.Log("[PinataDirector][Debug] Pinata session ended.");
    }

    // --- Quick-jump helpers to VERIFY FLAGS for 200–300 cycle ---

    [ContextMenu("Debug/Jump TotalDamage = 210 (cycle 200–300)")]
    private void Debug_JumpTo_210()
    {
        EnsureSessionForDebug();
        SetTotalDamageForDebug(210);
    }

    [ContextMenu("Debug/Jump TotalDamage = 280 (cycle 200–300)")]
    private void Debug_JumpTo_280()
    {
        EnsureSessionForDebug();
        SetTotalDamageForDebug(280);
    }

    // Also handy to test boundary rule (flag at end = 1.0)
    [ContextMenu("Debug/Jump TotalDamage = 700 (boundary)")]
    private void Debug_JumpTo_700()
    {
        EnsureSessionForDebug();
        SetTotalDamageForDebug(700);
    }

    // --- Generic helpers ---

    private void EnsureSessionForDebug()
    {
        if (!isRunning) Debug_BeginPinata_NoBoss();
    }

    private void SetTotalDamageForDebug(long targetTotal)
    {
        if (activeMeter == null)
        {
            Debug.LogError("[PinataDirector][Debug] Active meter is null. Start a session first.");
            return;
        }

        long current = activeMeter.TotalDamage;
        long delta = targetTotal - current;
        if (delta <= 0)
        {
            Debug.LogWarning($"[PinataDirector][Debug] Current TotalDamage ({current}) >= target ({targetTotal}). " +
                             $"Apply additional hits or restart session if you need exact totals.");
            return;
        }

        // Drive via ApplyHit so all events fire (UI, rewards, flags).
        // If you ever jump extremely large values, split into chunks; for 210/280/700 it's fine in one call.
        activeMeter.ApplyHit((int)delta);

        // Log state for quick verification
        int thr = activeMeter.Threshold;
        long cycleStart = (activeMeter.TotalDamage / thr) * thr;
        long cycleEnd = cycleStart + thr;

        float[] buf = new float[2];
        int count = PinataFlagUtility.GetCurrentCycleFlagPositions(activeMeter.TotalDamage, thr, 70, buf);
        string posText = count > 0 ? string.Join(", ", buf, 0, count) : "(none)";

        Debug.Log($"[PinataDirector][Debug] TotalDamage={activeMeter.TotalDamage}  " +
                  $"Cycle=[{cycleStart}-{cycleEnd})  Flags={count}  Pos={posText}  " +
                  $"Fill={(float)activeMeter.Current / thr:0.##}");
    }
    #endregion
#endif
}
