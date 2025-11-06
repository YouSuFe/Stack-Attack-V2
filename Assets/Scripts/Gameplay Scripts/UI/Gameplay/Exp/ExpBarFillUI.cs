using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated EXP bar that listens only to ExperienceSystem.OnExpChanged.
/// - Smoothly animates fill changes.
/// - When a level-up occurs (fill would go backwards), it wraps: animates -> 1, jumps to 0, animates -> target.
/// - Cancels any ongoing tween when a new update arrives (prevents stutter).
/// </summary>
public class ExpBarFillUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [Tooltip("ExperienceSystem to observe. If not set, will try ExperienceSystem.Instance.")]
    [SerializeField] private ExperienceSystem experienceSystem;

    [Tooltip("Image set to Filled type. Its fillAmount represents EXP progress.")]
    [SerializeField] private Image fillImage;

    [Header("Animation")]
    [Tooltip("Duration (in seconds) to animate a normal fill change.")]
    [SerializeField, Range(0.01f, 1.0f)] private float animateDuration = 0.18f;

    [Tooltip("Duration (in seconds) to animate the wrap to 1.0 when a level-up occurs.")]
    [SerializeField, Range(0.01f, 0.5f)] private float wrapToFullDuration = 0.10f;

    [Tooltip("Duration (in seconds) to animate from 0.0 to the post-level target fill.")]
    [SerializeField, Range(0.01f, 0.5f)] private float wrapFromZeroDuration = 0.12f;

    [Tooltip("Use unscaled time for UI animation (keeps animating if gameplay is paused).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Start State")]
    [Tooltip("Initialize the bar from current ExperienceSystem values on Awake.")]
    [SerializeField] private bool initializeOnAwake = true;
    #endregion

    #region Private State
    private Coroutine animateRoutine;
    private float currentFill = 0f;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (experienceSystem == null)
            experienceSystem = ExperienceSystem.Instance;

        if (fillImage == null)
            TryGetComponent(out fillImage);

        if (initializeOnAwake)
            SnapToSystem();
    }

    private void OnEnable()
    {
        if (experienceSystem != null)
            experienceSystem.OnExpChanged += HandleExpChanged;
    }

    private void OnDisable()
    {
        if (experienceSystem != null)
            experienceSystem.OnExpChanged -= HandleExpChanged;

        if (animateRoutine != null)
            StopCoroutine(animateRoutine);
        animateRoutine = null;
    }
    #endregion

    #region Event Handling
    private void HandleExpChanged(int currentExp, int nextThreshold)
    {
        if (fillImage == null || nextThreshold <= 0)
            return;

        float targetFill = Mathf.Clamp01(nextThreshold > 0 ? (float)currentExp / nextThreshold : 0f);

        // Detect wrap: after a level-up, currentEXP is leftover, and nextThreshold increased,
        // so the ratio typically goes DOWN compared to the previous ratio.
        bool wrapOccurred = targetFill < currentFill - 0.0001f;

        if (animateRoutine != null)
            StopCoroutine(animateRoutine);

        if (wrapOccurred)
        {
            // Sequence:
            // 1) Animate current -> 1.0 (finish the bar)
            // 2) Snap to 0.0
            // 3) Animate 0.0 -> targetFill
            animateRoutine = StartCoroutine(AnimateWrapSequence(targetFill));
        }
        else
        {
            animateRoutine = StartCoroutine(AnimateFill(currentFill, targetFill, animateDuration));
        }
    }
    #endregion

    #region Animation Coroutines
    private IEnumerator AnimateWrapSequence(float targetFill)
    {
        // Step 1: to full
        yield return AnimateFill(currentFill, 1f, wrapToFullDuration);

        // Step 2: snap to zero
        SetFillInstant(0f);

        // Step 3: up to new ratio
        yield return AnimateFill(currentFill, targetFill, wrapFromZeroDuration);

        animateRoutine = null;
    }

    private IEnumerator AnimateFill(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetFillInstant(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / duration);

            float value = Mathf.Lerp(from, to, SmoothStep(k));
            SetFillInstant(value);
            yield return null;
        }

        SetFillInstant(to);
    }
    #endregion

    #region Helpers
    private void SnapToSystem()
    {
        if (experienceSystem == null || fillImage == null) return;

        int next = Mathf.Max(1, experienceSystem.NextThreshold);
        float ratio = Mathf.Clamp01((float)experienceSystem.CurrentExp / next);
        SetFillInstant(ratio);
    }

    private void SetFillInstant(float value)
    {
        currentFill = Mathf.Clamp01(value);
        if (fillImage != null)
            fillImage.fillAmount = currentFill;
    }

    // Smoother than linear, but still cheap and allocation-free
    private static float SmoothStep(float x)
    {
        // Cubic smoothstep: 3x^2 - 2x^3
        return x * x * (3f - 2f * x);
    }
    #endregion

#if UNITY_EDITOR
    #region Debug
    [ContextMenu("Debug/Snap To System")]
    private void DebugSnap() => SnapToSystem();
    #endregion
#endif
}
