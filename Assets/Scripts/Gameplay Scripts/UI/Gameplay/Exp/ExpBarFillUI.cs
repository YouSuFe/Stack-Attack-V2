using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Purely visual EXP bar using a Filled Image.
/// - Assign a UI Image whose Type is set to Filled (Horizontal/Vertical/Radial as you like).
/// - Fills based on current EXP / threshold.
/// - No text, just the bar.
/// </summary>
public class ExpBarFillUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExperienceSystem experienceSystem;
    [SerializeField] private Image fillImage; // Image.type must be Filled

    [Header("Options")]
    [SerializeField, Range(0f, 1f)]
    private float minVisibleFill = 0f; // e.g., 0.05 to keep a tiny visible sliver at 0

    private void Awake()
    {
        if (experienceSystem == null)
            experienceSystem = ExperienceSystem.Instance;

        if (fillImage == null)
            Debug.LogError("[ExpBarFillUI] Fill Image reference not set.");

        if (fillImage != null && fillImage.type != Image.Type.Filled)
            Debug.LogWarning("[ExpBarFillUI] Image.type should be 'Filled' for proper behavior.");
    }

    private void OnEnable()
    {
        if (experienceSystem != null)
        {
            experienceSystem.OnExpChanged += OnExpChanged;
            experienceSystem.OnLeveledUp += OnLevelUp;

            // Initialize
            OnExpChanged(experienceSystem.CurrentExp, experienceSystem.NextThreshold);
        }
        else
        {
            Debug.LogWarning("[ExpBarFillUI] No ExperienceSystem available.");
        }
    }

    private void OnDisable()
    {
        if (experienceSystem != null)
        {
            experienceSystem.OnExpChanged -= OnExpChanged;
            experienceSystem.OnLeveledUp -= OnLevelUp;
        }
    }

    private void OnExpChanged(int current, int threshold)
    {
        if (fillImage == null || threshold <= 0) return;

        float t = Mathf.Clamp01((float)current / Mathf.Max(1, threshold));
        if (minVisibleFill > 0f) t = Mathf.Clamp01(Mathf.Lerp(minVisibleFill, 1f, t));
        fillImage.fillAmount = t;
    }

    private void OnLevelUp(int _)
    {
        // Update quickly to reflect carry-over
        if (experienceSystem != null)
            OnExpChanged(experienceSystem.CurrentExp, experienceSystem.NextThreshold);
    }
}
