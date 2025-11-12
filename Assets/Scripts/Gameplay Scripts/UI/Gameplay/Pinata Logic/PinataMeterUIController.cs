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
    #endregion

    #region Private
    private Coroutine animateRoutine;
    private readonly float[] flagPositions = new float[2];
    private const int REWARD_STEP = 70; // must match PinataMeter
    #endregion

    #region Public API
    /// <summary>Assigns a meter at runtime and (re)subscribes to its events.</summary>
    public void SetMeter(PinataMeter newMeter)
    {
        if (ReferenceEquals(meter, newMeter)) return;

        // Unhook old
        if (meter != null)
        {
            meter.OnValueChanged -= HandleValueChanged;
            meter.OnCyclePayout -= HandleCyclePayout;
            meter.OnPinataEnabledChanged -= HandleEnabledChanged;
            meter.OnRewardsGranted -= HandleRewardsGranted;
        }

        meter = newMeter;

        if (meter == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // Hook new
        meter.OnValueChanged += HandleValueChanged;
        meter.OnCyclePayout += HandleCyclePayout;
        meter.OnPinataEnabledChanged += HandleEnabledChanged;
        meter.OnRewardsGranted += HandleRewardsGranted;

        // Sync UI
        gameObject.SetActive(meter.IsEnabled);
        SnapFillToCurrent();
        RecomputeAndPlaceFlags();
    }
    #endregion

    #region Unity
    private void OnEnable()
    {
        if (fillImage == null || flagsOverlay == null)
        {
            Debug.LogWarning("[PinataMeterUIController] Missing references.");
            enabled = false;
            return;
        }

        // If meter was assigned in inspector, ensure subscriptions
        if (meter != null)
        {
            SetMeter(meter); // will subscribe + sync UI safely
        }
    }

    private void OnDisable()
    {
        if (meter == null) return;

        meter.OnValueChanged -= HandleValueChanged;
        meter.OnCyclePayout -= HandleCyclePayout;
        meter.OnPinataEnabledChanged -= HandleEnabledChanged;
        meter.OnRewardsGranted -= HandleRewardsGranted;

        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }
    }
    #endregion

    #region Event Handlers
    private void HandleEnabledChanged(bool enabledNow)
    {
        gameObject.SetActive(enabledNow);
    }

    private void HandleValueChanged(int current, int threshold)
    {
        float target = Mathf.Clamp01((float)current / Mathf.Max(1, threshold));
        SetFillAmount(target);
        RecomputeAndPlaceFlags();
    }

    private void HandleCyclePayout(int cycleIndex, int remainder)
    {
        float target = Mathf.Clamp01((float)remainder / Mathf.Max(1, meter.Threshold));
        SetFillAmount(target);
        RecomputeAndPlaceFlags();
    }

    private void HandleRewardsGranted(int count)
    {
        RecomputeAndPlaceFlags();
        // optional FX/SFX here
    }
    #endregion

    #region Fill Animation
    private void SnapFillToCurrent()
    {
        if (meter == null) return;
        float target = Mathf.Clamp01((float)meter.Current / Mathf.Max(1, meter.Threshold));
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }
        fillImage.fillAmount = target;
    }

    private void SetFillAmount(float target)
    {
        if (!animateFill || !isActiveAndEnabled)
        {
            if (animateRoutine != null)
            {
                StopCoroutine(animateRoutine);
                animateRoutine = null;
            }
            fillImage.fillAmount = target;
            return;
        }

        if (animateRoutine != null)
            StopCoroutine(animateRoutine);

        animateRoutine = StartCoroutine(AnimateFillCoroutine(target, fillAnimDuration));
    }

    private System.Collections.IEnumerator AnimateFillCoroutine(float target, float duration)
    {
        float start = fillImage.fillAmount;
        if (Mathf.Approximately(start, target))
        {
            fillImage.fillAmount = target;
            yield break;
        }

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            fillImage.fillAmount = Mathf.Lerp(start, target, t);
            yield return null;
        }
        fillImage.fillAmount = target;
        animateRoutine = null;
    }
    #endregion

    #region Flags
    private void RecomputeAndPlaceFlags()
    {
        if (meter == null) return;

        int count = PinataFlagUtility.GetCurrentCycleFlagPositions(
            meter.TotalDamage, meter.Threshold, REWARD_STEP, flagPositions
        );

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
        if (flag != null && flag.gameObject.activeSelf != active)
            flag.gameObject.SetActive(active);
    }

    private void PlaceFlag(RectTransform flag, float normalized)
    {
        if (flag == null || flagsOverlay == null) return;

        float width = flagsOverlay.rect.width;
        float pivotX = flagsOverlay.pivot.x;
        float anchoredX = (normalized * width) - (pivotX * width);

        Vector2 pos = flag.anchoredPosition;
        pos.x = anchoredX;
        flag.anchoredPosition = pos;
    }
    #endregion
}
