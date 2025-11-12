using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PinataMeterUIController : MonoBehaviour
{
    #region Serialized
    [Header("References")]
    [SerializeField, Tooltip("Pinata meter to observe (can be set at runtime).")]
    private PinataMeter meter;

    [SerializeField, Tooltip("Horizontal filled Image (type = Filled, FillMethod = Horizontal).")]
    private Image fillImage;

    [SerializeField, Tooltip("Overlay area matching the fill bar's RectTransform (same anchors/size).")]
    private RectTransform flagsOverlay;

    [SerializeField, Tooltip("First reusable flag icon.")]
    private RectTransform flagA;

    [SerializeField, Tooltip("Second reusable flag icon.")]
    private RectTransform flagB;

    [Header("Animation")]
    [SerializeField, Tooltip("Enable/disable fill animation.")]
    private bool animateFill = true;

    [SerializeField, Tooltip("Seconds for fast fill animation.")]
    [Range(0.05f, 0.35f)]
    private float fillAnimDuration = 0.15f;

    [Header("Debug")]
    [SerializeField, Tooltip("If true, prints detailed debug logs at critical points.")]
    private bool debugLogs = true;
    #endregion

    #region Private
    private Coroutine animateRoutine;
    private Coroutine delayedSyncRoutine;
    private readonly float[] flagPositions = new float[2];
    private const int REWARD_STEP = 70; // must match PinataMeter
    private bool subscribed;
    #endregion

    #region Public API
    /// <summary>Assigns a meter at runtime and (re)subscribes to its events.</summary>
    public void SetMeter(PinataMeter newMeter)
    {
        // Always unhook old (if any)
        if (subscribed && meter != null)
        {
            if (debugLogs) Debug.Log("[PinataUI] SetMeter: Unsubscribe OLD meter.", this);
            Unsubscribe(meter);
        }

        meter = newMeter;

        if (meter == null)
        {
            if (debugLogs) Debug.LogWarning("[PinataUI] SetMeter(null). Disabling UI root.", this);
            gameObject.SetActive(false);
            return;
        }

        // Always subscribe + sync even if it's "the same" meter reference
        if (debugLogs) Debug.Log("[PinataUI] SetMeter: Subscribe NEW meter (or same instance).", this);
        Subscribe(meter);

        gameObject.SetActive(meter.IsEnabled);
        SnapFillToCurrent();
        // Delay one frame so layout is valid (overlay width)
        StartDelayedSync();
    }
    #endregion

    #region Unity
    private void OnEnable()
    {
        if (fillImage == null || flagsOverlay == null)
        {
            Debug.LogWarning("[PinataMeterUIController] Missing references (fillImage or flagsOverlay). Disabling.");
            enabled = false;
            return;
        }

        if (debugLogs)
        {
            string fa = flagA ? flagA.name : "null";
            string fb = flagB ? flagB.name : "null";
            Debug.Log($"[PinataUI] OnEnable. meter={(meter ? meter.name : "null")} fillImage={fillImage.name} overlay={flagsOverlay.name} flagA={fa} flagB={fb}", this);
        }

        // If a meter is already assigned in the inspector, ensure we are subscribed & synced
        if (meter != null && !subscribed)
        {
            if (debugLogs) Debug.Log("[PinataUI] OnEnable → Subscribe to meter from inspector.", this);
            Subscribe(meter);
            gameObject.SetActive(meter.IsEnabled);
            SnapFillToCurrent();
            StartDelayedSync();
        }
        else if (meter != null)
        {
            // already subscribed, still ensure a delayed sync
            StartDelayedSync();
        }
    }

    private void OnDisable()
    {
        if (debugLogs) Debug.Log("[PinataUI] OnDisable. Unsubscribing and stopping coroutines.", this);

        if (subscribed && meter != null)
            Unsubscribe(meter);

        if (animateRoutine != null) { StopCoroutine(animateRoutine); animateRoutine = null; }
        if (delayedSyncRoutine != null) { StopCoroutine(delayedSyncRoutine); delayedSyncRoutine = null; }
    }
    #endregion

    #region Subscribe/Unsubscribe
    private void Subscribe(PinataMeter m)
    {
        if (m == null) return;
        m.OnValueChanged += HandleValueChanged;
        m.OnCyclePayout += HandleCyclePayout;
        m.OnPinataEnabledChanged += HandleEnabledChanged;
        m.OnRewardsGranted += HandleRewardsGranted;
        subscribed = true;

        if (debugLogs)
            Debug.Log($"[PinataUI] Subscribed. IsEnabled={m.IsEnabled}, Current={m.Current}, Threshold={m.Threshold}, TotalDamage={m.TotalDamage}", this);
    }

    private void Unsubscribe(PinataMeter m)
    {
        if (m == null) return;
        m.OnValueChanged -= HandleValueChanged;
        m.OnCyclePayout -= HandleCyclePayout;
        m.OnPinataEnabledChanged -= HandleEnabledChanged;
        m.OnRewardsGranted -= HandleRewardsGranted;
        subscribed = false;

        if (debugLogs)
            Debug.Log("[PinataUI] Unsubscribed.", this);
    }
    #endregion

    #region Event Handlers
    private void HandleEnabledChanged(bool enabledNow)
    {
        if (debugLogs) Debug.Log($"[PinataUI] HandleEnabledChanged → {enabledNow}", this);
        gameObject.SetActive(enabledNow);
        StartDelayedSync();
    }

    private void HandleValueChanged(int current, int threshold)
    {
        float target = Mathf.Clamp01((float)current / Mathf.Max(1, threshold));
        if (debugLogs) Debug.Log($"[PinataUI] HandleValueChanged: current={current}, threshold={threshold}, targetFill={target:0.###}", this);
        SetFillAmount(target);
        StartDelayedSync();
    }

    private void HandleCyclePayout(int cycleIndex, int remainder)
    {
        float target = Mathf.Clamp01((float)remainder / Mathf.Max(1, meter.Threshold));
        if (debugLogs) Debug.Log($"[PinataUI] HandleCyclePayout: cycleIndex={cycleIndex}, remainder={remainder}, targetFill={target:0.###}", this);
        SetFillAmount(target);
        StartDelayedSync();
    }

    private void HandleRewardsGranted(int count)
    {
        if (debugLogs) Debug.Log($"[PinataUI] HandleRewardsGranted: count={count}", this);
        StartDelayedSync();
    }
    #endregion

    #region Fill Animation
    private void SnapFillToCurrent()
    {
        if (meter == null)
        {
            if (debugLogs) Debug.LogWarning("[PinataUI] SnapFillToCurrent skipped (meter is null).", this);
            return;
        }
        float target = Mathf.Clamp01((float)meter.Current / Mathf.Max(1, meter.Threshold));
        if (animateRoutine != null)
        {
            if (debugLogs) Debug.Log("[PinataUI] SnapFillToCurrent: stopping existing animation coroutine.", this);
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }
        if (debugLogs) Debug.Log($"[PinataUI] SnapFillToCurrent → set fill to {target:0.###} (Current={meter.Current}, Threshold={meter.Threshold})", this);
        fillImage.fillAmount = target;
    }

    private void SetFillAmount(float target)
    {
        if (!animateFill || !isActiveAndEnabled)
        {
            if (animateRoutine != null)
            {
                if (debugLogs) Debug.Log("[PinataUI] SetFillAmount: animation disabled/invalid, stopping coroutine.", this);
                StopCoroutine(animateRoutine);
                animateRoutine = null;
            }
            if (debugLogs) Debug.Log($"[PinataUI] SetFillAmount (snap) → {target:0.###}", this);
            fillImage.fillAmount = target;
            return;
        }

        if (animateRoutine != null)
        {
            if (debugLogs) Debug.Log("[PinataUI] SetFillAmount: stopping previous animation.", this);
            StopCoroutine(animateRoutine);
        }

        if (debugLogs) Debug.Log($"[PinataUI] SetFillAmount: animating to {target:0.###} over {fillAnimDuration:0.###}s.", this);
        animateRoutine = StartCoroutine(AnimateFillCoroutine(target, fillAnimDuration));
    }

    private IEnumerator AnimateFillCoroutine(float target, float duration)
    {
        float start = fillImage.fillAmount;
        if (Mathf.Approximately(start, target))
        {
            if (debugLogs) Debug.Log("[PinataUI] AnimateFillCoroutine: start≈target, snapping.", this);
            fillImage.fillAmount = target;
            yield break;
        }

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        if (debugLogs) Debug.Log($"[PinataUI] AnimateFillCoroutine: from {start:0.###} to {target:0.###} (dur={duration:0.###})", this);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            fillImage.fillAmount = Mathf.Lerp(start, target, t);
            yield return null;
        }
        fillImage.fillAmount = target;
        animateRoutine = null;
        if (debugLogs) Debug.Log("[PinataUI] AnimateFillCoroutine: completed.", this);
    }
    #endregion

    #region Flags + Delayed Sync
    private void StartDelayedSync()
    {
        // Wait one frame so RectTransforms have proper size after activation/layout,
        // then recompute & place flags.
        if (delayedSyncRoutine != null) StopCoroutine(delayedSyncRoutine);
        delayedSyncRoutine = StartCoroutine(DelayedSync());
    }

    private IEnumerator DelayedSync()
    {
        yield return new WaitForEndOfFrame();
        RecomputeAndPlaceFlags();
        delayedSyncRoutine = null;
    }

    private void RecomputeAndPlaceFlags()
    {
        if (meter == null)
        {
            if (debugLogs) Debug.LogWarning("[PinataUI] RecomputeAndPlaceFlags skipped (meter is null).", this);
            return;
        }

        float width = flagsOverlay ? flagsOverlay.rect.width : 0f;
        if (debugLogs)
            Debug.Log($"[PinataUI] RecomputeAndPlaceFlags: TD={meter.TotalDamage}, Current={meter.Current}, Threshold={meter.Threshold}, OverlayWidth={width:0.##}", this);

        int count = PinataFlagUtility.GetCurrentCycleFlagPositions(
            meter.TotalDamage, meter.Threshold, REWARD_STEP, flagPositions
        );

        long thr = meter.Threshold;
        long start = (meter.TotalDamage / thr) * thr;
        long end = start + thr;

        if (debugLogs)
        {
            if (count == 0)
                Debug.Log($"[PinataUI] Flags result: count=0  Cycle=[{start}-{end})", this);
            else if (count == 1)
                Debug.Log($"[PinataUI] Flags result: count=1  pos0={flagPositions[0]:0.###}  Cycle=[{start}-{end})", this);
            else
                Debug.Log($"[PinataUI] Flags result: count=2  pos0={flagPositions[0]:0.###}, pos1={flagPositions[1]:0.###}  Cycle=[{start}-{end})", this);
        }

        switch (count)
        {
            case 0:
                SetFlagActive(flagA, false);
                SetFlagActive(flagB, false);
                break;
            case 1:
                SetFlagActive(flagA, true);
                PlaceFlag(flagA, flagPositions[0]);
                SetFlagActive(flagB, false);
                break;
            default:
                SetFlagActive(flagA, true);
                SetFlagActive(flagB, true);
                PlaceFlag(flagA, flagPositions[0]);
                PlaceFlag(flagB, flagPositions[1]);
                break;
        }
    }

    private void SetFlagActive(RectTransform flag, bool active)
    {
        if (flag == null) return;

        if (flag.gameObject.activeSelf != active)
        {
            if (debugLogs) Debug.Log($"[PinataUI] SetFlagActive: {flag.name} → {(active ? "ON" : "OFF")}", this);
            flag.gameObject.SetActive(active);
        }
        else
        {
            if (debugLogs) Debug.Log($"[PinataUI] SetFlagActive: {flag.name} already {(active ? "ON" : "OFF")}", this);
        }
    }

    private void PlaceFlag(RectTransform flag, float normalized)
    {
        if (flag == null || flagsOverlay == null)
        {
            if (debugLogs) Debug.LogWarning("[PinataUI] PlaceFlag skipped (flag or overlay is null).", this);
            return;
        }

        float width = flagsOverlay.rect.width;
        float pivotX = flagsOverlay.pivot.x;
        float anchoredX = (normalized * width) - (pivotX * width);

        Vector2 pos = flag.anchoredPosition;
        pos.x = anchoredX;
        flag.anchoredPosition = pos;

        if (debugLogs)
            Debug.Log($"[PinataUI] PlaceFlag: {flag.name}  normalized={normalized:0.###}  width={width:0.##}  pivotX={pivotX:0.##}  anchoredX={anchoredX:0.##}", this);
    }
    #endregion
}
