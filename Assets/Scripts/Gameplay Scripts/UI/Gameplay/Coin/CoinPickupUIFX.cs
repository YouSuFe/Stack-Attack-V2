using System.Collections;
using UnityEngine;

public sealed class CoinPickupUIFX : MonoBehaviour
{
    #region Constants
    private const int MAX_ICONS_PER_PICKUP = 10;
    #endregion

    #region Singleton
    public static CoinPickupUIFX Instance { get; private set; }
    #endregion

    #region Inspector
    [Header("References")]
    [Tooltip("Root Canvas RectTransform (Screen Space - Overlay or Screen Space - Camera).")]
    [SerializeField] private RectTransform canvasRect;

    [Tooltip("RectTransform of the HUD coin placeholder (destination).")]
    [SerializeField] private RectTransform coinTarget;

    [Tooltip("UI prefab to spawn (e.g., an Image with a coin sprite). Must have a RectTransform.")]
    [SerializeField] private GameObject coinIconPrefab;

    [Tooltip("Assign only if Canvas is Screen Space - Camera. Leave null for Overlay.")]
    [SerializeField] private Camera uiCamera; // null for Overlay

    [Header("Animation")]
    [Tooltip("Seconds each icon takes to fly to the HUD.")]
    [SerializeField, Range(0.05f, 1.5f)] private float travelTime = 0.45f;

    [Tooltip("Motion curve (0..1 time → 0..1 position).")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Random start jitter in screen-space to add liveliness.")]
    [SerializeField, Range(0f, 120f)] private float spawnJitter = 24f;

    [Tooltip("Visual icons per pickup (1..MAX).")]
    [SerializeField, Range(1, MAX_ICONS_PER_PICKUP)] private int iconsPerPickup = 5;

    [Tooltip("Delay between spawning each visual icon (seconds).")]
    [SerializeField, Range(0f, 0.2f)] private float iconStagger = 0.04f;

    [Header("Debug (Play Mode)")]
    [SerializeField] private bool debugEnabled = false;
    [SerializeField] private KeyCode debugKey = KeyCode.C;
    [SerializeField, Range(0, MAX_ICONS_PER_PICKUP)] private int debugIconsOverride = 0;
    #endregion

    #region Private
    private Transform activeParent; // usually the Canvas while visible
    #endregion

    #region Unity
    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (canvasRect == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = canvas.transform as RectTransform;
        }

        activeParent = canvasRect != null ? canvasRect : transform;
    }

    private void Update()
    {
        if (!debugEnabled) return;
        if (Input.GetKeyDown(debugKey)) DebugSpawnFromRandomScreen();
    }
    #endregion

    #region Public API
    /// <summary>
    /// Purely visual: spawn up to N icons from worldPosition and fly them to coinTarget.
    /// </summary>
    public void PlayFromWorld(Vector3 worldPosition, int visualIconCountOverride = 0)
    {
        if (activeParent == null || coinIconPrefab == null || coinTarget == null) return;

        var cam = uiCamera != null ? uiCamera : Camera.main;

        // Convert world → local canvas space
        if (!WorldToCanvasLocal(worldPosition, cam, out var localStart)) return;

        // Target (end) in local canvas space
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)activeParent,
            RectTransformUtility.WorldToScreenPoint(cam, coinTarget.position),
            cam,
            out var localEnd
        );

        int spawnCount = visualIconCountOverride > 0
            ? Mathf.Clamp(visualIconCountOverride, 1, MAX_ICONS_PER_PICKUP)
            : Mathf.Clamp(iconsPerPickup, 1, MAX_ICONS_PER_PICKUP);

        StartCoroutine(SpawnBurst(localStart, localEnd, spawnCount));
    }
    #endregion

    #region Internals
    private IEnumerator SpawnBurst(Vector2 localStart, Vector2 localEnd, int spawnCount)
    {
        for (int i = 0; i < spawnCount; i++)
        {
            // Instantiate under canvas so it's visible immediately
            var icon = Instantiate(coinIconPrefab, activeParent);
            var rt = icon.transform as RectTransform;

            // Jittered start
            Vector2 jitter = Random.insideUnitCircle * spawnJitter;
            rt.anchoredPosition = localStart + jitter;
            rt.localScale = Vector3.one;
            icon.SetActive(true);

            StartCoroutine(FlyRoutine(icon, rt, localEnd, travelTime));

            if (iconStagger > 0f) yield return new WaitForSeconds(iconStagger);
        }
    }

    private IEnumerator FlyRoutine(GameObject icon, RectTransform rt, Vector2 localEnd, float time)
    {
        float t = 0f;
        Vector2 start = rt.anchoredPosition;

        while (t < time)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / time);
            float curved = moveCurve.Evaluate(n);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, localEnd, curved);
            yield return null;
        }

        rt.anchoredPosition = localEnd;

        // Purely visual: destroy when finished
        Destroy(icon);
    }

    private bool WorldToCanvasLocal(Vector3 world, Camera cam, out Vector2 local)
    {
        var canvasRT = (RectTransform)activeParent;
        if (cam != null)
        {
            Vector2 screen = cam.WorldToScreenPoint(world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screen, cam, out local);
        }
        else
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screen, null, out local);
        }
    }
    #endregion

    #region Debug Helpers
    [ContextMenu("DEBUG/Spawn From Random Screen Position")]
    public void DebugSpawnFromRandomScreen()
    {
        if (activeParent == null || coinTarget == null || coinIconPrefab == null) return;

        // Random screen point in current resolution (Overlay-safe)
        Vector2 screen = new Vector2(Random.Range(0f, Screen.width), Random.Range(0f, Screen.height));
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)activeParent, screen, null, out var localStart))
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)activeParent,
            RectTransformUtility.WorldToScreenPoint(null, coinTarget.position),
            null,
            out var localEnd
        );

        int spawnCount = (debugIconsOverride > 0)
            ? Mathf.Clamp(debugIconsOverride, 1, MAX_ICONS_PER_PICKUP)
            : Mathf.Clamp(iconsPerPickup, 1, MAX_ICONS_PER_PICKUP);

        StartCoroutine(SpawnBurst(localStart, localEnd, spawnCount));
    }
    #endregion
}
