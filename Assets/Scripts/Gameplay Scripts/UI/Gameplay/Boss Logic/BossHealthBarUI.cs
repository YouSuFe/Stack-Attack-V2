using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal screen-space boss health bar:
/// - Filled Image only (no text)
/// - Hidden by default; shows on Fight, hides on Pinata
/// - Updates fill based on BossHealth
/// - Must be bound at runtime via Bind(controller, health)
/// Place on a GameObject under a HUD Canvas. Assign a Filled Image (Horizontal).
/// </summary>
[DisallowMultipleComponent]
public class BossHealthBarUI : MonoBehaviour
{
    #region Serialized
    [Header("Bindings")]
    [SerializeField, Tooltip("Image that visually fills based on health. Image.type=Filled, Fill Method=Horizontal.")]
    private Image fillImage;

    [Header("Visuals")]
    [SerializeField, Range(0.01f, 10f), Tooltip("How fast the bar lerps to the new value.")]
    private float fillLerpSpeed = 6f;

    [SerializeField, Range(0f, 2f), Tooltip("Seconds to fade in/out if a CanvasGroup is present.")]
    private float fadeDuration = 0.2f;
    #endregion

    #region Private Fields
    private BossStateController controller;
    private BossHealth bossHealth;
    private CanvasGroup canvasGroup; // optional
    private Coroutine fadeRoutine;
    private float targetFill = 1f;
    private bool subscribed;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Optional CanvasGroup for fades
        TryGetComponent(out canvasGroup);

        // Safe default
        SetFillImmediate(1f);          // start full (in case you preview in editor)
        SetVisibleImmediate(false);    // hidden until Fight
    }

    private void OnDisable()
    {
        Unsubscribe();
    }
    #endregion

    #region Public API
    /// <summary>
    /// Bind references after boss is spawned. Call once per boss instance.
    /// </summary>
    public void Bind(BossStateController newController, BossHealth newHealth)
    {
        Unsubscribe();

        controller = newController;
        bossHealth = newHealth;

        if (!controller || !bossHealth)
        {
            Debug.LogWarning("[BossHealthBarUI] Bind failed: controller or health is null.");
            return;
        }

        Subscribe();

        // Initialize fill to current health (stay hidden until Fight state arrives)
        ApplyHealth(bossHealth.CurrentHealth, bossHealth.MaxHealth, immediate: true);
        SetVisibleImmediate(false);
    }
    #endregion

    #region Subscribe / Unsubscribe
    private void Subscribe()
    {
        if (subscribed) return;

        bossHealth.OnDamaged += HandleDamaged;
        controller.OnStateChanged += HandleStateChanged;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        if (bossHealth != null) bossHealth.OnDamaged -= HandleDamaged;
        if (controller != null) controller.OnStateChanged -= HandleStateChanged;

        subscribed = false;
    }
    #endregion

    #region Event Handlers
    private void HandleDamaged(int current, int max)
    {
        ApplyHealth(current, max, immediate: false);
    }

    private void HandleStateChanged(BossStateController.BossState state)
    {
        // Always visible only during Fight; hidden otherwise (incl. Pinata)
        bool shouldShow = state == BossStateController.BossState.Fight;
        FadeVisible(shouldShow);
    }
    #endregion

    #region UI Logic
    private void ApplyHealth(int current, int max, bool immediate)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);
        float pct = (float)current / max;
        ApplyFill01(pct, immediate);
    }

    private void ApplyFill01(float pct, bool immediate)
    {
        pct = Mathf.Clamp01(pct);
        targetFill = pct;

        if (fillImage == null) return;

        if (immediate)
        {
            SetFillImmediate(targetFill);
        }
        else
        {
            StopCoroutineFill();
            StartCoroutine(LerpFillRoutine());
        }
    }

    private IEnumerator LerpFillRoutine()
    {
        while (fillImage != null && !Mathf.Approximately(fillImage.fillAmount, targetFill))
        {
            float t = Mathf.Clamp01(fillLerpSpeed * Time.unscaledDeltaTime);
            fillImage.fillAmount = Mathf.Lerp(fillImage.fillAmount, targetFill, t);
            if (Mathf.Abs(fillImage.fillAmount - targetFill) <= 0.001f)
                fillImage.fillAmount = targetFill;
            yield return null;
        }
    }

    private void FadeVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            gameObject.SetActive(visible);
            return;
        }

        if (fadeDuration <= 0f)
        {
            SetVisibleImmediate(visible);
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(visible ? 1f : 0f));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float start = canvasGroup.alpha;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        bool visible = targetAlpha > 0.001f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        if (!visible) gameObject.SetActive(false);
        else gameObject.SetActive(true);
        fadeRoutine = null;
    }
    #endregion

    #region Helpers
    private void SetFillImmediate(float value)
    {
        if (fillImage != null) fillImage.fillAmount = Mathf.Clamp01(value);
    }

    private void SetVisibleImmediate(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            gameObject.SetActive(true); // keep object alive so we can fade later
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    private void StopCoroutineFill()
    {
        // We only start one unnamed LerpFillRoutine at a time; StopAllCoroutines would also cancel fades.
        // Simpler approach: rely on the next Lerp setting to converge; no ref kept.
        // (Intentionally empty: using pattern that avoids killing fade coroutine.)
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("UI/Validate Setup")]
    private void Editor_Validate()
    {
        if (fillImage == null) Debug.LogWarning("[BossHealthBarUI] Fill Image not assigned.");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
#endif
}
