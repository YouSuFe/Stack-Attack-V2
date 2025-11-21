using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// Controls a dead/KO screen with a revive and a not-revive (give up) option,
/// plus a countdown that auto-selects "not revive" at 0.
/// - No fancy animations; shows immediately.
/// - Integer countdown (e.g., 5,4,3,2,1,0).
/// - Image fill drains with time (1 → 0).
/// - Works with unscaled time (typical if gameplay is paused on death).
/// </summary>
[DisallowMultipleComponent]
public class DeadCanvasController : MonoBehaviour
{
    #region Serialized Fields
    [Header("UI References")]
    [SerializeField, Tooltip("Button the player presses to revive.")]
    private Button reviveButton;

    [SerializeField, Tooltip("Button the player presses to not revive / give up.")]
    private Button notReviveButton;

    [SerializeField, Tooltip("TMP label that shows remaining seconds.")]
    private TMP_Text timerText;

    [SerializeField, Tooltip("Image whose fillAmount drains with the countdown (set Image.Type to Filled).")]
    private Image timerFillImage;

    [Header("Behavior")]
    [SerializeField, Tooltip("Default countdown duration in seconds.")]
    private int defaultCountdownSeconds = 5;

    [SerializeField, Tooltip("If true, countdown uses real time (ignores Time.timeScale).")]
    private bool useUnscaledTime = true;

    [SerializeField, Tooltip("Hide the canvas automatically after a choice is made or timeout.")]
    private bool hideOnDecision = true;

    [Header("Audio")]
    [SerializeField, Tooltip("Played when the dead canvas is shown.")]
    private SoundData deadCanvasSound;
    #endregion

    #region Public API
    /// <summary>Raised when Revive is chosen.</summary>
    public event Action ReviveRequested;

    /// <summary>Raised when Not Revive is chosen or timeout.</summary>
    public event Action NotReviveRequested;

    /// <summary>Current remaining seconds (integer display value).</summary>
    public int RemainingSeconds => remainingSeconds;
    #endregion

    #region Private State
    private Coroutine countdownRoutine;
    private int remainingSeconds;
    private int initialSeconds;
    private bool decisionMade;
    #endregion

    #region Unity
    private void Awake()
    {
        if (reviveButton != null) reviveButton.onClick.AddListener(HandleReviveClicked);
        if (notReviveButton != null) notReviveButton.onClick.AddListener(HandleNotReviveClicked);

        // Safety: ensure fill image is actually "Filled"
        if (timerFillImage != null && timerFillImage.type != Image.Type.Filled)
            timerFillImage.type = Image.Type.Filled;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Shows this canvas (GameObject must be enabled externally or be part of an always-on canvas).
    /// Starts countdown from the provided seconds (or default if null).
    /// </summary>
    public void Show(int? seconds = null)
    {
        gameObject.SetActive(true);
        Debug.Log("DebugCanvasController : Trying to set game object active true");

        if (deadCanvasSound != null)
            SoundUtils.Play2D(deadCanvasSound);

        StartCountdown(seconds ?? defaultCountdownSeconds);
    }

    /// <summary>
    /// Hides this canvas.
    /// </summary>
    public void Hide()
    {
        StopCountdown();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Start/restart the countdown (resets UI).
    /// </summary>
    public void StartCountdown(int seconds)
    {
        StopCountdown();

        decisionMade = false;
        initialSeconds = Mathf.Max(0, seconds);
        remainingSeconds = initialSeconds;

        UpdateTimerUI(force: true);

        Debug.Log("DebugCanvasController : Trying to update UI before starting timer");

        if (initialSeconds > 0)
            countdownRoutine = StartCoroutine(CountdownRoutine());
        else
            TimeoutToNotRevive();
    }
    #endregion

    #region Button Handlers
    private void HandleReviveClicked()
    {
        if (decisionMade) return;
        decisionMade = true;

        Debug.Log("[DeadCanvasController] Revive chosen.");
        DisableButtons();

        ReviveRequested?.Invoke();

        if (hideOnDecision) Hide();
    }

    private void HandleNotReviveClicked()
    {
        if (decisionMade) return;
        decisionMade = true;

        Debug.Log("[DeadCanvasController] Not Revive chosen.");
        DisableButtons();

        NotReviveRequested?.Invoke();

        if (hideOnDecision) Hide();
    }
    #endregion

    #region Countdown
    private IEnumerator CountdownRoutine()
    {
        // Prime UI fully visible (e.g., "5" and 100% fill) on the first rendered frame.
        UpdateTimerUI(force: true);
        yield return null; // let the activation frame settle to avoid a big first delta

        // Anchor timing using absolute timestamps; this is resilient to frame hitches.
        float start = useUnscaledTime ? Time.realtimeSinceStartup : Time.time;

        while (!decisionMade)
        {
            float now = useUnscaledTime ? Time.realtimeSinceStartup : Time.time;
            float elapsed = Mathf.Max(0f, now - start);
            float timeLeft = Mathf.Max(0f, initialSeconds - elapsed);

            // Smooth fill every frame
            UpdateTimerFillOnly(timeLeft);

            // Integer label (hold current second until it fully elapses)
            int toShow = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
            if (toShow != remainingSeconds)
            {
                remainingSeconds = toShow;
                UpdateTimerUI(force: true);
            }

            if (timeLeft <= 0f)
                break;

            yield return null;
        }

        countdownRoutine = null;

        if (!decisionMade)
            TimeoutToNotRevive();
    }

    private void StopCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }
    }
    #endregion

    #region UI Updates
    private void UpdateTimerUI(bool force = false)
    {
        // Update text
        if (timerText != null)
            timerText.text = Mathf.Clamp(remainingSeconds, 0, 999).ToString();

        // Update fill (snaps to integer step here; smooth fill handled per-frame in the coroutine)
        float t = initialSeconds <= 0 ? 0f : Mathf.Clamp01((float)remainingSeconds / initialSeconds);
        if (timerFillImage != null)
            timerFillImage.fillAmount = t;

        if (force)
        {
            // Hook for tick SFX or haptics if needed.
        }
    }

    private void UpdateTimerFillOnly(float timeLeft)
    {
        if (timerFillImage == null || initialSeconds <= 0) return;
        float t = Mathf.Clamp01(timeLeft / initialSeconds);
        timerFillImage.fillAmount = t;
    }
    #endregion

    #region Helpers
    private void DisableButtons()
    {
        if (reviveButton != null) reviveButton.interactable = false;
        if (notReviveButton != null) notReviveButton.interactable = false;
    }

    private void TimeoutToNotRevive()
    {
        // Snap to 0 in UI
        remainingSeconds = 0;
        UpdateTimerUI(force: true);

        Debug.Log("[DeadCanvasController] Timer reached 0 → Not Revive.");
        HandleNotReviveClicked();
    }
    #endregion
}
