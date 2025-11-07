using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SpawnStageAgent : MonoBehaviour
{
    public enum StageState { Staging, Handoff, Active }

    public event Action OnHandoffCompleted;

    #region Serialized
    [Header("Staging / Layers")]
    [Tooltip("Layer used while off-screen (ignored by gameplay collisions).")]
    [SerializeField] private LayerMask stagingLayer;

    [Tooltip("Use prefab's original layer at handoff start.")]
    [SerializeField] private bool usePrefabLayerAsGameplay = true;

    [Tooltip("Optional explicit gameplay layer override (ignored if above is true).")]
    [SerializeField] private LayerMask gameplayLayerOverride;

    [Header("Handoff Defaults")]
    [SerializeField] private float defaultDragDuration = 0.5f;

    [SerializeField, Tooltip("Ease curve for the Y handoff motion (0..1).")]
    private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Category / Tagging (optional)")]
    [SerializeField] private SpawnCategory category = SpawnCategory.Generic;
    public enum SpawnCategory { Generic, Enemy, Coin, Trap, Collectable, Multiplier, PowerUp, Pivot }
    #endregion

    #region Private
    private IStageActivatable[] activatables;
    private Coroutine handoffRoutine;
    private StageState state = StageState.Staging;
    private bool initialized;

    // NEW: remember the prefab/authored layer so we can restore it at DRAG START.
    private int prefabAuthoredLayer;
    #endregion

    public StageState State => state;
    public SpawnCategory Category => category;

    private void Awake()
    {
        RefreshActivatables(); // okay if none yet
    }

    private void OnEnable()
    {
        // Cache the prefab/authored layer BEFORE we change to staging
        prefabAuthoredLayer = gameObject.layer;
        RefreshActivatables(); // MovementDefinition may have just added movers
        EnterStagingState();
    }

    private void OnDisable()
    {
        CancelHandoffIfAny();
        if (StagingConveyor.Instance != null)
            StagingConveyor.Instance.Unregister(this);
    }

    private void OnDestroy()
    {
        CancelHandoffIfAny();
        if (StagingConveyor.Instance != null)
            StagingConveyor.Instance.Unregister(this);
    }

    private void EnterStagingState()
    {
        state = StageState.Staging;

        // Put on Staging layer so off-screen collisions don't happen
        int stagingIndex = LayerMaskToLayer(stagingLayer);
        if (stagingIndex != -1)
            gameObject.layer = stagingIndex;

        // Pause movers while off-screen
        foreach (var a in activatables) a.PauseMover();

        if (StagingConveyor.Instance != null)
            StagingConveyor.Instance.Register(this);
        else
            Debug.LogWarning("[SpawnStageAgent] Missing StagingConveyor in scene." + gameObject.name);
    }

    private void CancelHandoffIfAny()
    {
        if (handoffRoutine != null)
        {
            StopCoroutine(handoffRoutine);
            handoffRoutine = null;
        }
    }

    /// <summary>
    /// Called by EntryGate when the object touches the gate trigger.
    /// IMPORTANT: switches to final gameplay layer *immediately*, then performs the drag,
    /// so enemies can be damaged / items can be collected during the drag.
    /// </summary>
    public void BeginHandoff(float targetY, float durationOverride = -1f)
    {
        if (state != StageState.Staging) return;

        // Leave the conveyor now
        if (StagingConveyor.Instance != null)
            StagingConveyor.Instance.Unregister(this);

        state = StageState.Handoff;

        // === LAYER SWITCH HAPPENS HERE (at drag START) ===
        int gameplayLayer = prefabAuthoredLayer;

        if (!usePrefabLayerAsGameplay)
        {
            int overrideLayer = LayerMaskToLayer(gameplayLayerOverride);
            if (overrideLayer != -1)
                gameplayLayer = overrideLayer;
        }

        gameObject.layer = gameplayLayer;
        // === From this moment, object participates in gameplay collisions (during drag). ===

        RefreshActivatables();

        float duration = durationOverride > 0f ? durationOverride : defaultDragDuration;

        if (TryGetComponent<PivotAnchor>(out var anchor))
        {
            anchor.StartGroupHandoff(transform.position.y, targetY, duration, easeCurve);
        }

        handoffRoutine = StartCoroutine(HandoffRoutine(targetY, duration));
    }

    private IEnumerator HandoffRoutine(float targetY, float duration)
    {
        Vector3 start = transform.position;
        Vector3 end = new Vector3(start.x, targetY, start.z);
        float t = 0f;

        while (t < duration)
        {
            float u = easeCurve.Evaluate(t / duration);
            transform.position = Vector3.LerpUnclamped(start, end, u);
            t += Time.deltaTime;
            yield return null;
        }

        transform.position = end;

        // Arm & resume movers immediately at the end of the drag (no frame gap)
        foreach (var a in activatables) a.ArmAtEntry(transform.position);
        foreach (var a in activatables) a.ResumeMover();

        state = StageState.Active;
        handoffRoutine = null;
        OnHandoffCompleted?.Invoke();
    }

    public void BeginHandoffTo(Vector3 targetWorldPos, float durationOverride = -1f, AnimationCurve curveOverride = null)
    {
        if (state != StageState.Staging) return;

        // Leave conveyor
        StagingConveyor.Instance?.Unregister(this);
        state = StageState.Handoff;

        // === Switch to gameplay layer at drag START (same policy as BeginHandoff) ===
        int gameplayLayer = prefabAuthoredLayer;
        if (!usePrefabLayerAsGameplay)
        {
            int overrideLayer = LayerMaskToLayer(gameplayLayerOverride);
            if (overrideLayer != -1) gameplayLayer = overrideLayer;
        }
        gameObject.layer = gameplayLayer;

        // Resolve duration / curve
        float duration = durationOverride > 0f ? durationOverride : defaultDragDuration;
        var curve = curveOverride ?? easeCurve;

        // Run your existing Vector3 end-position routine
        StartCoroutine(HandoffRoutinePosition(targetWorldPos, duration, curve));
    }

    /// <summary>
    /// Start a synchronized handoff by dragging this object DOWN by the same deltaY as the pivot.
    /// Used when the pivot begins the group handoff; followers may not be at the gate yet.
    /// </summary>
    public void BeginSynchronizedHandoffDelta(float deltaY, float duration, AnimationCurve curveOverride = null)
    {
        if (state != StageState.Staging) return;

        // leave the conveyor
        StagingConveyor.Instance?.Unregister(this);
        state = StageState.Handoff;

        // switch to gameplay layer at drag start so this unit can interact during drag
        int gameplayLayerToUse = gameObject.layer; // prefab-authored layer (we cached earlier)
        if (!usePrefabLayerAsGameplay)
        {
            int overrideLayer = LayerMaskToLayer(gameplayLayerOverride);
            if (overrideLayer != -1) gameplayLayerToUse = overrideLayer;
        }
        gameObject.layer = gameplayLayerToUse;

        // compute end position by delta
        Vector3 start = transform.position;
        Vector3 end = new Vector3(start.x, start.y + deltaY, start.z);

        // run the same coroutine path (localized here)
        StartCoroutine(HandoffRoutinePosition(end, duration, curveOverride ?? easeCurve));
    }

    // small variant of the routine that drags to a Vector3 end (not just Y)
    private IEnumerator HandoffRoutinePosition(Vector3 end, float duration, AnimationCurve curve)
    {
        Vector3 start = transform.position;
        float t = 0f;

        while (t < duration)
        {
            float u = curve.Evaluate(t / duration);
            transform.position = Vector3.LerpUnclamped(start, end, u);
            t += Time.deltaTime;
            yield return null;
        }

        transform.position = end;

        // Arm & resume movers immediately at the end of the drag
        RefreshActivatables(); // ensure we have latest movers
        foreach (var a in activatables) a.ArmAtEntry(transform.position);
        foreach (var a in activatables) a.ResumeMover();

        state = StageState.Active;
        OnHandoffCompleted?.Invoke();
    }


    private void RefreshActivatables()
    {
        activatables = GetComponents<IStageActivatable>();
    }

    private static int LayerMaskToLayer(LayerMask mask)
    {
        int value = mask.value;
        if (value == 0) return -1;
        for (int i = 0; i < 32; i++)
        {
            if ((value & (1 << i)) != 0)
                return i;
        }
        return -1;
    }
}
