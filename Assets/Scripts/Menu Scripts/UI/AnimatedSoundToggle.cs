using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated left-right sound toggle.
/// - Reads/writes SoundToggleService.
/// - Smoothly animates knob position & colors.
/// - Uses left/right anchor RectTransforms for layout-safe positions.
/// - Auto-syncs across scenes (menu + gameplay).
/// </summary>
[RequireComponent(typeof(Button))]
public class AnimatedSoundToggle : MonoBehaviour
{
    #region Inspector Fields

    [Header("UI References")]
    [SerializeField, Tooltip("Background image of the toggle.")]
    private Image backgroundImage;

    [SerializeField, Tooltip("Knob/circle Image RectTransform that slides left/right.")]
    private RectTransform knobTransform;

    [Header("Anchors")]
    [SerializeField, Tooltip("Knob position when OFF.")]
    private RectTransform leftAnchor;

    [SerializeField, Tooltip("Knob position when ON.")]
    private RectTransform rightAnchor;

    [Header("Colors")]
    [SerializeField, Tooltip("Background color when OFF.")]
    private Color backgroundOffColor = new Color(0.6f, 0.6f, 0.6f);

    [SerializeField, Tooltip("Background color when ON.")]
    private Color backgroundOnColor = new Color(0.2f, 0.9f, 0.4f);

    [SerializeField, Tooltip("Knob color when OFF.")]
    private Color knobOffColor = new Color(0.8f, 0.8f, 0.8f);

    [SerializeField, Tooltip("Knob color when ON.")]
    private Color knobOnColor = Color.white;

    [Header("Animation")]
    [SerializeField, Range(0.05f, 0.5f), Tooltip("Seconds for the slide animation.")]
    private float animationDuration = 0.15f;

    [SerializeField, Tooltip("If true, uses unscaled time (works while game paused).")]
    private bool useUnscaledTime = true;

    #endregion

    #region Private Fields

    private Button button;
    private Coroutine animRoutine;
    private Image knobImage;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        button = GetComponent<Button>();
        knobImage = knobTransform != null ? knobTransform.GetComponent<Image>() : null;

        button.onClick.AddListener(OnToggleClicked);

        // Ensure global state is loaded
        SoundToggleService.EnsureInitialized();

        // Apply initial visual without animation
        ApplyVisualInstant(SoundToggleService.IsSoundEnabled);
    }

    private void OnEnable()
    {
        SoundToggleService.OnSoundStateChanged += HandleSoundStateChanged;
    }

    private void OnDisable()
    {
        SoundToggleService.OnSoundStateChanged -= HandleSoundStateChanged;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnToggleClicked);
    }

    #endregion

    #region Events

    private void OnToggleClicked()
    {
        // Toggle global sound. All toggles update via event.
        SoundToggleService.Toggle();
    }

    private void HandleSoundStateChanged(bool enabled)
    {
        AnimateToState(enabled);
    }

    #endregion

    #region Visuals

    private void ApplyVisualInstant(bool enabled)
    {
        if (knobTransform != null && leftAnchor != null && rightAnchor != null)
            knobTransform.position = enabled ? rightAnchor.position : leftAnchor.position;

        if (backgroundImage != null)
            backgroundImage.color = enabled ? backgroundOnColor : backgroundOffColor;

        if (knobImage != null)
            knobImage.color = enabled ? knobOnColor : knobOffColor;
    }

    private void AnimateToState(bool enabled)
    {
        if (!IsReadyForAnimation())
        {
            ApplyVisualInstant(enabled);
            return;
        }

        if (animRoutine != null)
            StopCoroutine(animRoutine);

        animRoutine = StartCoroutine(AnimateRoutine(enabled));
    }

    private IEnumerator AnimateRoutine(bool enabled)
    {
        Vector3 startPos = knobTransform.position;
        Vector3 targetPos = enabled ? rightAnchor.position : leftAnchor.position;

        Color startBg = backgroundImage.color;
        Color targetBg = enabled ? backgroundOnColor : backgroundOffColor;

        Color startKnob = knobImage.color;
        Color targetKnob = enabled ? knobOnColor : knobOffColor;

        float t = 0f;

        while (t < animationDuration)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(t / animationDuration);

            // Smooth ease-out curve
            float eased = 1f - Mathf.Pow(1f - u, 3f);

            knobTransform.position = Vector3.Lerp(startPos, targetPos, eased);
            backgroundImage.color = Color.Lerp(startBg, targetBg, eased);
            knobImage.color = Color.Lerp(startKnob, targetKnob, eased);

            yield return null;
        }

        ApplyVisualInstant(enabled);
        animRoutine = null;
    }

    private bool IsReadyForAnimation()
    {
        return knobTransform != null
            && backgroundImage != null
            && knobImage != null
            && leftAnchor != null
            && rightAnchor != null;
    }

    #endregion
}
