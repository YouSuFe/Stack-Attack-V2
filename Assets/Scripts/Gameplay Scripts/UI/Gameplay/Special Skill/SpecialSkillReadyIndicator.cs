using UnityEngine;
using System.Collections;

/// <summary>
/// Shows an animated "Release" UI when the special skill is fully charged.
/// UI uses CanvasGroup + scale pulsing animation.
/// </summary>
public class SpecialSkillReadyIndicator : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField, Tooltip("Reference to the SpecialSkillDriver on the player.")]
    private SpecialSkillDriver skillDriver;

    [SerializeField, Tooltip("The world-space UI root object that contains the CanvasGroup and Text.")]
    private GameObject indicatorRoot;

    [SerializeField, Tooltip("CanvasGroup for fade/visibility control.")]
    private CanvasGroup canvasGroup;

    [SerializeField, Tooltip("How fast the text pulses.")]
    [Range(0.1f, 4f)]
    private float pulseSpeed = 2f;

    [SerializeField, Tooltip("Pulse size multiplier.")]
    [Range(1f, 2f)]
    private float pulseScale = 1.15f;
    #endregion

    #region Private Fields
    private Coroutine pulseRoutine;
    private Vector3 initialScale;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (skillDriver == null)
            TryGetComponent(out skillDriver);

        if (indicatorRoot != null)
            indicatorRoot.SetActive(false);

        if (canvasGroup == null && indicatorRoot != null)
            canvasGroup = indicatorRoot.GetComponent<CanvasGroup>();

        if (indicatorRoot != null)
            initialScale = indicatorRoot.transform.localScale;
    }

    private void OnEnable()
    {
        if (skillDriver == null) return;

        skillDriver.OnChargeChanged += HandleChargeChanged;
        skillDriver.OnSkillActivated += HandleSkillActivated;
        skillDriver.OnSkillEnded += HandleSkillEnded;
    }

    private void OnDisable()
    {
        if (skillDriver == null) return;

        skillDriver.OnChargeChanged -= HandleChargeChanged;
        skillDriver.OnSkillActivated -= HandleSkillActivated;
        skillDriver.OnSkillEnded -= HandleSkillEnded;

        StopPulse();
    }
    #endregion

    #region Event Handlers
    private void HandleChargeChanged(int current, int required)
    {
        if (indicatorRoot == null) return;

        bool ready = current >= required;

        if (ready)
            ShowIndicator();
        else
            HideIndicator();
    }

    private void HandleSkillActivated()
    {
        HideIndicator();
    }

    private void HandleSkillEnded()
    {
        HideIndicator();
    }
    #endregion

    #region UI Control
    private void ShowIndicator()
    {
        if (indicatorRoot == null) return;

        indicatorRoot.SetActive(true);

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        StartPulse();
    }

    private void HideIndicator()
    {
        if (indicatorRoot == null) return;

        indicatorRoot.SetActive(false);
        StopPulse();
    }
    #endregion

    #region Animation
    private void StartPulse()
    {
        StopPulse();
        pulseRoutine = StartCoroutine(PulseAnimation());
    }

    private void StopPulse()
    {
        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = null;

        if (indicatorRoot != null)
            indicatorRoot.transform.localScale = initialScale;
    }

    private IEnumerator PulseAnimation()
    {
        float timer = 0f;

        while (true)
        {
            timer += Time.deltaTime * pulseSpeed;

            float scale = Mathf.Lerp(1f, pulseScale, (Mathf.Sin(timer) + 1f) * 0.5f);
            indicatorRoot.transform.localScale = initialScale * scale;

            yield return null;
        }
    }
    #endregion
}
