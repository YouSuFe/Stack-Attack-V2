using UnityEngine;

#region Summary
/// <summary>
/// Fits a full-screen RectTransform to the device safe area (notch, rounded corners, home indicator).
/// Place this on a full-screen, stretch-anchored container under your Canvas,
/// then put the rest of your UI (e.g., announcer root) inside this container.
///
/// Features:
/// - Per-axis conform (X/Y)
/// - Ignore only bottom inset (useful when you want a background overlapping the home indicator)
/// - Extra padding (L,T,R,B) after safe-area inset
/// - Editor-only simulation via custom rect
/// - Auto-updates on resolution/orientation change (runtime + editor)
/// </summary>
#endregion
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    #region Serialized Fields
    [Header("Conform")]
    [SerializeField, Tooltip("Apply the safe-area horizontally (left/right).")]
    private bool applyHorizontal = true;

    [SerializeField, Tooltip("Apply the safe-area vertically (top/bottom).")]
    private bool applyVertical = true;

    [SerializeField, Tooltip("Ignore ONLY the bottom inset (keeps top notch inset).")]
    private bool ignoreBottom = false;

    [Header("Extra Padding (px)")]
    [SerializeField, Tooltip("Extra padding AFTER safe-area inset. Order: Left, Top, Right, Bottom (pixels).")]
    private Vector4 extraPadding = Vector4.zero; // L, T, R, B

    [Header("Logging")]
    [SerializeField, Tooltip("Print details whenever the safe area is applied.")]
    private bool verboseLogs = false;
    #endregion

    #region Editor Simulation
#if UNITY_EDITOR
    [Header("Editor Simulation (Optional)")]
    [SerializeField, Tooltip("Enable to simulate a safe area in the Editor Game view.")]
    private bool simulateInEditor = false;

    [SerializeField, Tooltip("Custom safe-area rectangle in absolute pixels (Game view). Leave at 0 to use Screen.safeArea.")]
    private Rect editorSafeAreaOverride = Rect.zero;
#endif
    #endregion

    #region Private Fields
    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private ScreenOrientation lastOrientation;

    // Prevent OnValidate from writing layout immediately
    private bool pendingReapply;

    // Prevent re-entrancy when layout change triggers callbacks while we are already applying
    private bool isApplying;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Initialize();
        // Do not apply here if you see platform-specific warnings; Update will handle it
        pendingReapply = true;
    }

    private void OnEnable()
    {
        pendingReapply = true;
    }

    private void Update()
    {
        // Runs in Edit Mode (ExecuteAlways). Consolidate all changes here to avoid OnValidate warnings.
        if (pendingReapply || ScreenSizeChanged() || OrientationChanged())
        {
            pendingReapply = false;
            ApplySafeAreaIfChanged(force: true);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        // Called both in Edit Mode and Play Mode. Defer application to Update to avoid re-entrancy.
        pendingReapply = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // DO NOT touch RectTransform here; just mark for later.
        pendingReapply = true;
    }
#endif
    #endregion

    #region Core Logic
    private void Initialize()
    {
        if (!rectTransform)
            rectTransform = GetComponent<RectTransform>();

        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;
        lastSafeArea = GetCurrentSafeArea(); // seed
    }

    private void ApplySafeAreaIfChanged(bool force)
    {
        if (isApplying) return; // guard
        isApplying = true;

        var safe = GetCurrentSafeArea();

        bool sizeChanged = ScreenSizeChanged();
        bool orientChanged = OrientationChanged();
        bool safeChanged = (safe != lastSafeArea);

        if (force || sizeChanged || orientChanged || safeChanged)
        {
            ApplySafeAreaInternal(safe);
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastOrientation = Screen.orientation;
            lastSafeArea = safe;
        }

        isApplying = false;
    }

    private bool ScreenSizeChanged()
    {
        Vector2Int now = new Vector2Int(Screen.width, Screen.height);
        return now != lastScreenSize;
    }

    private bool OrientationChanged()
    {
        return Screen.orientation != lastOrientation;
    }

    private Rect GetCurrentSafeArea()
    {
#if UNITY_EDITOR
        if (simulateInEditor && editorSafeAreaOverride.width > 0f && editorSafeAreaOverride.height > 0f)
        {
            return ClampToScreen(editorSafeAreaOverride);
        }
#endif
        return ClampToScreen(Screen.safeArea);
    }

    private Rect ClampToScreen(Rect r)
    {
        float x = Mathf.Clamp(r.x, 0f, Screen.width);
        float y = Mathf.Clamp(r.y, 0f, Screen.height);
        float w = Mathf.Clamp(r.width, 0f, Screen.width - x);
        float h = Mathf.Clamp(r.height, 0f, Screen.height - y);
        return new Rect(x, y, w, h);
    }

    private void ApplySafeAreaInternal(Rect safe)
    {
        // Respect per-axis toggles
        if (!applyHorizontal)
        {
            safe.x = 0f;
            safe.width = Screen.width;
        }

        if (!applyVertical)
        {
            safe.y = 0f;
            safe.height = Screen.height;
        }

        // Optionally ignore only the bottom inset (keep top notch inset)
        if (ignoreBottom)
        {
            safe = new Rect(safe.x, 0f, safe.width, safe.y + safe.height);
        }

        // Extra padding after safe-area inset (L,T,R,B in pixels)
        safe.xMin += extraPadding.x; // Left
        safe.yMax -= extraPadding.y; // Top
        safe.xMax -= extraPadding.z; // Right
        safe.yMin += extraPadding.w; // Bottom

        // Clamp again after padding
        safe = ClampToScreen(safe);

        // Convert to normalized anchors
        Vector2 anchorMin = new Vector2(
            Screen.width > 0 ? safe.xMin / Screen.width : 0f,
            Screen.height > 0 ? safe.yMin / Screen.height : 0f);

        Vector2 anchorMax = new Vector2(
            Screen.width > 0 ? safe.xMax / Screen.width : 1f,
            Screen.height > 0 ? safe.yMax / Screen.height : 1f);

        // Defensive against NaNs during early startup on some devices
        if (!float.IsFinite(anchorMin.x) || !float.IsFinite(anchorMin.y) ||
            !float.IsFinite(anchorMax.x) || !float.IsFinite(anchorMax.y))
            return;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;

        // Zero offsets so anchors fully drive layout
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        if (verboseLogs)
        {
            Debug.Log(
                $"[SafeAreaFitter] Applied anchors min={anchorMin} max={anchorMax} | " +
                $"Screen=({Screen.width}x{Screen.height}) Safe=({safe.x},{safe.y},{safe.width},{safe.height})",
                this);
        }
    }
    #endregion
}
