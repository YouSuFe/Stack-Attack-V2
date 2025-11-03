using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Announces the NEXT gameplay segment while you are inside a Space segment.
/// - You place bespoke UI children (inactive) under uiRoot (one per type: Enemy, Reward, Boss, etc.).
/// - On Space segment START: looks ahead and shows the mapped child for the next non-Space segment.
/// - On Space segment END: hides the UI.
/// - Anim: scales ONLY the active child (0→1 with overshoot) + fades CanvasGroup. Parent rects are untouched.
/// - Uses unscaled time. No pooling. No prefabs.
/// </summary>
public class UpcomingSegmentAnnouncer : MonoBehaviour
{
    #region Serialized Fields
    [Header("Hooks")]
    [SerializeField, Tooltip("Sequencer that raises OnSegmentStarted/OnSegmentEnded.")]
    private LevelSegmentSequencer sequencer;

    [SerializeField, Tooltip("The LevelDefinition that the sequencer uses. Needed to look ahead.")]
    private LevelDefinition levelDefinition;

    [SerializeField, Tooltip("Parent RectTransform that contains all unique reminder children.")]
    private RectTransform uiRoot;

    [SerializeField, Tooltip("CanvasGroup on uiRoot (used for fade). Will be added if missing.")]
    private CanvasGroup canvasGroup;

    [Header("Mappings")]
    [SerializeField, Tooltip("Map SegmentType -> UI child under uiRoot (inactive by default).")]
    private List<SegmentUIMap> maps = new();

    [Header("Animation")]
    [SerializeField, Tooltip("Seconds to fade in (CanvasGroup) and scale child 0→1")]
    [Range(0.05f, 0.75f)] private float inDuration = 0.18f;

    [SerializeField, Tooltip("Seconds to fade out (CanvasGroup) and scale child 1→0")]
    [Range(0.05f, 0.75f)] private float outDuration = 0.22f;

    [SerializeField, Tooltip("Slight overshoot on child pop-in (0 = none)")]
    [Range(0f, 0.3f)] private float popOvershoot = 0.12f;

    [Header("Behavior")]
    [SerializeField, Tooltip("If a new Space starts while a UI is still showing, interrupt and switch immediately.")]
    private bool interruptOnNewSpace = true;

    [SerializeField, Tooltip("Log useful messages in the Console.")]
    private bool verboseLogs = false;
    #endregion

    #region Private State
    private readonly Dictionary<SegmentType, GameObject> byType = new();
    private Coroutine playRoutine;
    private GameObject currentChild;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (!uiRoot) Debug.LogError("[UpcomingSegmentAnnouncer] uiRoot is not assigned.");
        if (!sequencer) Debug.LogError("[UpcomingSegmentAnnouncer] sequencer is not assigned.");
        if (!levelDefinition) Debug.LogWarning("[UpcomingSegmentAnnouncer] levelDefinition not assigned; look-ahead requires it.");

        BuildLookup();
        EnsureCanvasGroup();
        HideImmediate();
        DeactivateAllChildren();
    }

    private void OnEnable()
    {
        if (sequencer != null)
        {
            sequencer.OnSegmentStarted += HandleSegmentStarted;
            sequencer.OnSegmentEnded += HandleSegmentEnded;
        }
    }

    private void OnDisable()
    {
        if (sequencer != null)
        {
            sequencer.OnSegmentStarted -= HandleSegmentStarted;
            sequencer.OnSegmentEnded -= HandleSegmentEnded;
        }
    }
    #endregion

    #region Event Handlers
    private void HandleSegmentStarted(int index, LevelSegment segment)
    {
        if (!segment) return;

        // Only react on Space start: announce what's coming next.
        if (segment.SegmentType != SegmentType.Space)
            return;

        var nextType = FindNextNonSpaceType(index);
        if (nextType == null)
        {
            if (verboseLogs) Debug.Log("[Announcer] No next non-space segment found.");
            HideImmediate();
            return;
        }

        if (!byType.TryGetValue(nextType.Value, out var child) || child == null)
        {
            if (verboseLogs) Debug.LogWarning($"[Announcer] No mapped UI for upcoming type '{nextType.Value}'.");
            HideImmediate();
            return;
        }

        Show(child);
    }

    private void HandleSegmentEnded(int index, LevelSegment segment)
    {
        if (!segment) return;

        // Only hide when the Space we were in ends.
        if (segment.SegmentType == SegmentType.Space)
        {
            HideAnimated();
        }
    }
    #endregion

    #region Core Show/Hide
    private void Show(GameObject child)
    {
        // Interrupt current animation if needed
        if (playRoutine != null)
        {
            if (interruptOnNewSpace)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
                if (currentChild) currentChild.SetActive(false);
            }
            else
            {
                if (verboseLogs) Debug.Log("[Announcer] Ignored Show (busy and interrupt disabled).");
                return;
            }
        }

        // Activate target child and deactivate others (we scale only this child)
        ActivateOnly(child);
        currentChild = child;

        // Reset visuals: fade=0, child scale=0 (parent transforms untouched)
        canvasGroup.alpha = 0f;
        if (currentChild.TryGetComponent<RectTransform>(out var childRect))
            childRect.localScale = Vector3.zero;

        // Animate in (fade + scale child)
        playRoutine = StartCoroutine(AnimateInChildAndFade());
    }

    private void HideAnimated()
    {
        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(AnimateOutChildAndFade(() =>
        {
            if (currentChild)
                currentChild.SetActive(false);
            currentChild = null;
        }));
    }

    private void HideImmediate()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        canvasGroup.alpha = 0f;

        // Ensure any active child is disabled and scaled back to zero
        if (currentChild)
        {
            if (currentChild.TryGetComponent<RectTransform>(out var childRect))
                childRect.localScale = Vector3.zero;
            currentChild.SetActive(false);
            currentChild = null;
        }
    }
    #endregion

    #region Animation (scale only the active child, fade the CanvasGroup)
    private IEnumerator AnimateInChildAndFade()
    {
        float duration = Mathf.Max(0.01f, inDuration);
        float t = 0f;

        // child scale from 0 -> (1 + overshoot), then snap to 1 at end
        float overshoot = Mathf.Clamp01(popOvershoot);
        Vector3 startScale = Vector3.zero;
        Vector3 overshootScale = Vector3.one * (1f + overshoot);

        RectTransform childRect = null;
        if (currentChild) currentChild.TryGetComponent(out childRect);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);

            // EaseOutBack for scale
            float eased = EaseOutBack(u);

            if (childRect != null)
                childRect.localScale = Vector3.LerpUnclamped(startScale, overshootScale, eased);

            canvasGroup.alpha = u; // linear fade is fine here
            yield return null;
        }

        // Snap to exact scale 1 on complete
        if (childRect != null)
            childRect.localScale = Vector3.one;

        canvasGroup.alpha = 1f;
        playRoutine = null;
    }

    private IEnumerator AnimateOutChildAndFade(System.Action onDone)
    {
        float duration = Mathf.Max(0.01f, outDuration);
        float t = 0f;

        RectTransform childRect = null;
        if (currentChild) currentChild.TryGetComponent(out childRect);

        Vector3 startScale = Vector3.one;
        Vector3 endScale = Vector3.zero;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);

            // Ease-in for exit
            float eased = u * u;

            if (childRect != null)
                childRect.localScale = Vector3.Lerp(startScale, endScale, eased);

            canvasGroup.alpha = 1f - u;
            yield return null;
        }

        if (childRect != null)
            childRect.localScale = Vector3.zero;

        canvasGroup.alpha = 0f;
        onDone?.Invoke();
        playRoutine = null;
    }

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
    }
    #endregion

    #region Helpers
    private void BuildLookup()
    {
        byType.Clear();
        for (int i = 0; i < maps.Count; i++)
        {
            var m = maps[i];
            if (m != null && m.target != null)
                byType[m.segmentType] = m.target;
        }
    }

    private void EnsureCanvasGroup()
    {
        if (!canvasGroup && uiRoot)
            uiRoot.TryGetComponent(out canvasGroup);

        if (!canvasGroup && uiRoot)
            canvasGroup = uiRoot.gameObject.AddComponent<CanvasGroup>();
    }

    private void DeactivateAllChildren()
    {
        if (!uiRoot) return;
        for (int i = 0; i < uiRoot.childCount; i++)
            uiRoot.GetChild(i).gameObject.SetActive(false);
    }

    private void ActivateOnly(GameObject child)
    {
        if (!uiRoot) return;
        for (int i = 0; i < uiRoot.childCount; i++)
        {
            var go = uiRoot.GetChild(i).gameObject;
            go.SetActive(go == child);
        }
    }

    /// <summary>
    /// Find the next non-Space segment type after the given index.
    /// </summary>
    private SegmentType? FindNextNonSpaceType(int currentIndex)
    {
        if (!levelDefinition || levelDefinition.Segments == null) return null;

        var list = levelDefinition.Segments;
        for (int i = currentIndex + 1; i < list.Count; i++)
        {
            var seg = list[i];
            if (seg && seg.SegmentType != SegmentType.Space)
                return seg.SegmentType;
        }
        // Default, fallback for enemywave
        return SegmentType.EnemyWave;
    }

    public void SetLevelDefinition(LevelDefinition def)
    {
        levelDefinition = def;
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Show Enemy (as upcoming)")]
    private void DebugShowEnemy()
    {
        if (byType.TryGetValue(SegmentType.EnemyWave, out var child)) Show(child);
    }

    [ContextMenu("Debug/Show Reward (as upcoming)")]
    private void DebugShowReward()
    {
        if (byType.TryGetValue(SegmentType.Reward, out var child)) Show(child);
    }

    [ContextMenu("Debug/Show Boss (as upcoming)")]
    private void DebugShowBoss()
    {
        if (byType.TryGetValue(SegmentType.Boss, out var child)) Show(child);
    }

    [ContextMenu("Debug/Hide Now")]
    private void DebugHide()
    {
        HideImmediate();
    }
#endif
}

/// <summary>
/// Map one game-specific SegmentType to a specific UI GameObject under uiRoot.
/// </summary>
[System.Serializable]
public class SegmentUIMap
{
    [Tooltip("Which upcoming segment type this UI represents.")]
    public SegmentType segmentType;

    [Tooltip("The UI child GameObject under uiRoot. Customize freely (icons, TMP sizes, layout).")]
    public GameObject target;
}
