using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class DamageFlashUIController : MonoBehaviour
{
#region Serialized Fields
    [Header("References")]
    [Tooltip("Reference to the player's health script.")]
    [SerializeField] private PlayerHealth playerHealth;

    [Tooltip("CanvasGroup controlling flash opacity. If left null, will auto-grab from this GameObject.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Flash Settings")]
    [Tooltip("How quickly the flash fades in (seconds).")]
    [SerializeField, Range(0.01f, 1f)] private float fadeInDuration = 0.1f;

    [Tooltip("How long the flash stays visible before fading out (seconds).")]
    [SerializeField, Range(0f, 2f)] private float holdDuration = 0.1f;

    [Tooltip("How quickly the flash fades out (seconds).")]
    [SerializeField, Range(0.05f, 2f)] private float fadeOutDuration = 0.5f;

    [Tooltip("Maximum alpha of the flash.")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.6f;

    [Header("Behavior")]
    [Tooltip("If true, a new damage event snaps to max alpha instantly before continuing the animation.")]
    [SerializeField] private bool snapToMaxOnRehit = true;
#endregion

#region Private Fields
    private Coroutine flashRoutine;
#endregion

#region Unity Lifecycle
    private void Awake()
    {
        if (canvasGroup == null)
            TryGetComponent(out canvasGroup);

        if (canvasGroup == null)
        {
            Debug.LogError("[DamageFlashUIController] Missing CanvasGroup.");
            enabled = false;
            return;
        }

        // Start invisible and non-blocking
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDamaged += HandlePlayerDamaged;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDamaged -= HandlePlayerDamaged;
    }
#endregion

#region Event Handlers
    private void HandlePlayerDamaged(int currentHearts, int damage, GameObject source)
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }
#endregion

#region Coroutines
    private IEnumerator FlashRoutine()
    {
        // Optional snap to max on re-hit (stronger feedback)
        if (snapToMaxOnRehit && canvasGroup.alpha < maxAlpha)
            canvasGroup.alpha = maxAlpha;

        // Fade in (pause-aware)
        yield return FadeRoutine(targetAlpha: maxAlpha, duration: fadeInDuration);

        // Hold (pause-aware via helper)
        if (holdDuration > 0f)
            yield return PauseAwareCoroutine.Delay(holdDuration);

        // Fade out (pause-aware)
        yield return FadeRoutine(targetAlpha: 0f, duration: fadeOutDuration);

        flashRoutine = null;
    }

    /// <summary>
    /// Pause-aware alpha tween using scaled time. Halts fully while PauseManager says gameplay stopped.
    /// </summary>
    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (canvasGroup == null || duration <= 0f)
        {
            if (canvasGroup != null) canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // If globally paused, hang cleanly (independent of Time.timeScale policy).
            while (PauseManager.Instance != null && PauseManager.Instance.IsGameplayStopped)
                yield return null;

            float dt = Time.deltaTime;      // if timeScale=0, this is 0 → we hang here naturally
            elapsed += dt;

            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
#endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Trigger Flash")]
    private void DebugTrigger() => HandlePlayerDamaged(0, 1, null);
#endif
}
