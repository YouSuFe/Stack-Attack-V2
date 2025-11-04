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

    [Header("Events")]
    [SerializeField, Tooltip("Invoked when Revive is chosen.")]
    private UnityEvent onRevive;

    [SerializeField, Tooltip("Invoked when Not Revive is chosen or timeout.")]
    private UnityEvent onNotRevive;
    #endregion

    #region Public API (optional C# events)
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

        onRevive?.Invoke();
        ReviveRequested?.Invoke();

        if (hideOnDecision) Hide();
    }

    private void HandleNotReviveClicked()
    {
        if (decisionMade) return;
        decisionMade = true;

        Debug.Log("[DeadCanvasController] Not Revive chosen.");
        DisableButtons();

        onNotRevive?.Invoke();
        NotReviveRequested?.Invoke();

        if (hideOnDecision) Hide();
    }
    #endregion

    #region Countdown
    private IEnumerator CountdownRoutine()
    {
        float timeLeft = initialSeconds;   // float for smooth fill
        int lastShown = -1;

        while (!decisionMade && timeLeft > 0f)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            timeLeft = Mathf.Max(0f, timeLeft - delta);

            // Update integer display when it changes (floor)
            int toShow = Mathf.FloorToInt(timeLeft);
            if (toShow != lastShown)
            {
                remainingSeconds = toShow;
                UpdateTimerUI(force: true);
                lastShown = toShow;
            }
            else
            {
                // Still update fill every frame for smoothness
                UpdateTimerFillOnly(timeLeft);
            }

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

        // Update fill (smooth)
        float t = initialSeconds <= 0 ? 0f : Mathf.Clamp01((float)remainingSeconds / initialSeconds);
        if (timerFillImage != null)
            timerFillImage.fillAmount = t;

        if (force)
        {
            // No-op for now; placeholder if you want to add SFX or tick feedback at each second.
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
