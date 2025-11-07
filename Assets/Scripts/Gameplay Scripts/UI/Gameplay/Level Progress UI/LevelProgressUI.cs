using System;
using UnityEngine;
using UnityEngine.UI;

public class LevelProgressUI : MonoBehaviour
{
    #region Serialized Fields
    [Header("Runtime Source")]
    [SerializeField, Tooltip("Reference to the LevelProgressRuntime in the scene.")]
    private LevelProgressRuntime runtime;

    [Header("Fill Bar")]
    [SerializeField, Tooltip("UI Image that shows overall progress (will be configured to Filled/Vertical/Bottom).")]
    private Image fillImage;

    [Header("Markers UI")]
    [SerializeField, Tooltip("RectTransform with anchors (0,0)-(1,1) and pivot (0.5, 0.0) BOTTOM.")]
    private RectTransform markersContainer;

    [SerializeField, Tooltip("Prefab with RectTransform pivot (0.5, 0.0) BOTTOM.")]
    private GameObject starMarkerPrefab;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        // Subscribe to initialization so we can build markers when runtime is ready.
        if (runtime != null)
            runtime.OnInitialized += HandleRuntimeInitialized;
    }

    private void OnDisable()
    {
        if (runtime != null)
            runtime.OnInitialized -= HandleRuntimeInitialized;
    }

    private void Start()
    {
        if (!ValidateRefs())
        {
            enabled = false;
            return;
        }

        ConfigureFillImage();

        // Try to build immediately (will early-out if runtime isn’t ready yet).
        BuildMarkersUI();

        // Initialize fill once.
        UpdateFill();
    }

    private void Update()
    {
        UpdateFill();
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// Called when the runtime signals that its plan/distances are ready.
    /// </summary>
    private void HandleRuntimeInitialized()
    {
        BuildMarkersUI();
        UpdateFill();
    }
    #endregion

    #region Fill
    /// <summary>
    /// Sets the fill Image to bottom->top and clears the amount.
    /// </summary>
    private void ConfigureFillImage()
    {
        if (fillImage == null) return;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Vertical;
        fillImage.fillOrigin = (int)Image.OriginVertical.Bottom; // bottom -> top
        fillImage.fillAmount = 0f;
    }

    /// <summary>
    /// Applies runtime.Progress01 to the fill bar. If runtime isn’t ready yet, this
    /// simply shows 0 and will update automatically later.
    /// </summary>
    private void UpdateFill()
    {
        if (fillImage == null || runtime == null) return;
        fillImage.fillAmount = Mathf.Clamp01(runtime.Progress01);
    }
    #endregion

    #region Markers
    /// <summary>
    /// Instantiates stars at Enemy/Reward/Boss starts along the container height.
    /// 
    /// Container & Prefab Layout Assumptions (as per your setup):
    ///   - markersContainer: anchors (0,0)-(1,1), pivot (0.5, 0.0)  => bottom pivot
    ///   - starMarkerPrefab: RectTransform pivot (0.5, 0.0)         => bottom pivot
    /// 
    /// Mapping math (per-segment GAP model):
    ///   gap = runtime.GapSpawnToReference  (spawnY - referenceY)
    ///   distanceToMarker = (segmentIndex * gap) + (startRow * rowHeight)
    ///   t = distanceToMarker / runtime.TotalDistanceWorld      ∈ [0..1]
    ///   anchoredY = t * markersContainer.rect.height           (0=bottom, 1=top)
    /// 
    /// We place stars for EnemyWave / Reward / Boss and stop at the FIRST Boss
    /// so the top corresponds to the boss start.
    /// </summary>
    private void BuildMarkersUI()
    {
        if (markersContainer == null || starMarkerPrefab == null || runtime == null)
            return;

        var plan = runtime.Plan;
        if (plan.Markers == null || plan.Markers.Length == 0)
            return;

        float total = runtime.TotalDistanceWorld;
        if (total <= 0.0001f)
            return;

        // Make sure layout size is valid before reading height.
        LayoutRebuilder.ForceRebuildLayoutImmediate(markersContainer);
        float containerHeight = markersContainer.rect.height;

        // Clear previous stars.
        for (int i = markersContainer.childCount - 1; i >= 0; i--)
            Destroy(markersContainer.GetChild(i).gameObject);

        float gap = runtime.GapSpawnToReference;
        float rowH = runtime.RowHeight;

        for (int m = 0; m < plan.Markers.Length; m++)
        {
            var marker = plan.Markers[m];

            // Only show EnemyWave / Reward / Boss.
            if (marker.Type != SegmentType.EnemyWave &&
                marker.Type != SegmentType.Reward &&
                marker.Type != SegmentType.Boss)
                continue;

            // Stop at FIRST Boss: UI spans only to boss START.
            if (marker.Type == SegmentType.Boss && marker.SegmentIndex > plan.SegmentsBeforeBossStart)
                break;

            // Accumulate GAP per prior segment and world height per prior rows.
            int k = marker.SegmentIndex;
            float distanceToMarker = (k * gap) + (marker.StartRow * rowH);

            // Normalize along the total span (boss start => t=1).
            float t = Mathf.Clamp01(distanceToMarker / total);

            // Bottom-pivot container mapping: y grows upward from bottom.
            float anchoredY = t * containerHeight;

            // Instantiate star and force bottom-centered anchors & pivot so y=0 is bottom edge.
            var go = Instantiate(starMarkerPrefab, markersContainer);
            if (!go.TryGetComponent<RectTransform>(out var rt))
                rt = go.AddComponent<RectTransform>(); // safety

            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);

            // Center on X; place on Y.
            rt.anchoredPosition = new Vector2(0f, anchoredY);

            // If this is the FIRST Boss marker, we are done.
            if (marker.Type == SegmentType.Boss) break;
        }
    }
    #endregion

    #region Utilities
    private bool ValidateRefs()
    {
        bool ok = true;

        if (runtime == null) { Debug.LogError("LevelProgressUI: Runtime is not assigned."); ok = false; }
        if (fillImage == null) { Debug.LogError("LevelProgressUI: Fill Image is not assigned."); ok = false; }

        // Markers are optional; only warn if one of them is provided without the other.
        if (markersContainer == null ^ starMarkerPrefab == null)
            Debug.LogWarning("LevelProgressUI: To show stars, assign BOTH markersContainer and starMarkerPrefab.");

        return ok;
    }
    #endregion
}
