using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PivotAnchor : MonoBehaviour, IPausable
{
    #region Static Registry
    private static readonly Dictionary<string, PivotAnchor> Registry = new();

    public static event Action<string, PivotAnchor> AnchorRegistered;
    public static event Action<string, PivotAnchor> AnchorUnregistered;

    public static bool TryGet(string key, out PivotAnchor a) => Registry.TryGetValue(key, out a);

    public static PivotAnchor GetOrCreate(string key, Vector3 worldPos)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (Registry.TryGetValue(key, out var existing) && existing) return existing;

        var go = new GameObject($"Anchor_{key}");
        var created = go.AddComponent<PivotAnchor>();
        created.anchorKey = key;
        created.transform.position = worldPos;
        Registry[key] = created;
        AnchorRegistered?.Invoke(key, created);
        return created;
    }

    /// <summary>
    /// Preferred path: create or return an existing anchor, using a prefab if we need to instantiate.
    /// The prefab should already include SpawnStageAgent, desired tag (e.g., "Pivot"), and layer setup.
    /// </summary>
    public static PivotAnchor GetOrCreate(string key, Vector3 worldPos, PivotAnchor prefab)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (Registry.TryGetValue(key, out var existing) && existing) return existing;

        PivotAnchor created;
        if (prefab != null)
        {
            created = Instantiate(prefab, worldPos, Quaternion.identity);
        }
        else
        {
            var go = new GameObject($"Anchor_{key}");
            created = go.AddComponent<PivotAnchor>();
            created.transform.position = worldPos;
        }

        created.anchorKey = key;
        Registry[key] = created;
        AnchorRegistered?.Invoke(key, created);
        return created;
    }

    public static PivotAnchor Find(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        Registry.TryGetValue(key, out var a);
        return a;
    }
    #endregion

    #region Inspector
    [SerializeField] private string anchorKey;

    [Tooltip("Angular speed (deg/sec) applied to the entire formation.")]
    [SerializeField, Range(0f, 720f)] private float angularSpeedDegPerSec = 90f;

    [Tooltip("Downward drift applied to the entire formation (units/sec). Applied only AFTER activation.")]
    [SerializeField, Range(0f, 50f)] private float verticalSpeed = 0f;
    #endregion

    #region Public State / Events
    public float AngleDeg { get; private set; }

    /// <summary>True AFTER entry gate handoff (or immediately if no SpawnStageAgent present).</summary>
    public bool IsActivated { get; private set; } = false;

    /// <summary>Set when pivot begins the synchronized handoff; cleared on activation.</summary>
    public bool GroupHandoffStarted { get; private set; } = false;

    public event Action OnActivated;
    #endregion

    #region Cache / Followers
    private SpawnStageAgent cachedAgent;   // cached reference if present
    private bool subscribed;               // guard to avoid double-subscribing
    private bool isPaused;

    private readonly List<OrbitAroundAnchorMover> followers = new List<OrbitAroundAnchorMover>();
    public void RegisterFollower(OrbitAroundAnchorMover f)
    {
        if (f != null && !followers.Contains(f))
            followers.Add(f);
    }
    public void UnregisterFollower(OrbitAroundAnchorMover f)
    {
        if (f != null) followers.Remove(f);
    }
    #endregion

    #region Unity
    private void Awake()
    {
        // Registry bookkeeping only. DO NOT decide activation here (order with SpawnStageAgent is undefined).
        if (!string.IsNullOrEmpty(anchorKey))
        {
            Registry[anchorKey] = this;
            AnchorRegistered?.Invoke(anchorKey, this);
        }
    }

    private void OnEnable()
    {
        if (PauseManager.Instance != null)
            PauseManager.Instance.Register(this);
        TryWireAgentAndActivation();
    }

    private void OnDisable()
    {
        if (PauseManager.Instance != null)
            PauseManager.Instance.Unregister(this);

        if (cachedAgent != null && subscribed)
        {
            cachedAgent.OnHandoffCompleted -= HandleAgentHandoffCompleted;
            subscribed = false;
        }
    }

    private void Update()
    {
        if (isPaused) return;

        // Formation rotation always ticks
        AngleDeg += angularSpeedDegPerSec * Time.deltaTime;

        // Apply vertical drift ONLY after activation (avoid double-move with conveyor)
        if (IsActivated && verticalSpeed > 0f)
            transform.position += Vector3.down * (verticalSpeed * Time.deltaTime);
    }
    #endregion

    #region Activation Wiring
    private void TryWireAgentAndActivation()
    {
        // Agent may be added before or after us — handle both cases
        if (!TryGetComponent(out cachedAgent))
            cachedAgent = null;

        if (cachedAgent == null)
        {
            // No staging → scene-placed, immediate active
            ActivateNow();
            return;
        }

        if (cachedAgent.State == SpawnStageAgent.StageState.Active)
        {
            // Already finished handoff
            ActivateNow();
        }
        else
        {
            if (!subscribed)
            {
                cachedAgent.OnHandoffCompleted += HandleAgentHandoffCompleted;
                subscribed = true;
            }
            IsActivated = false; // ensure off until handoff completes
        }
    }

    private void HandleAgentHandoffCompleted()
    {
        if (cachedAgent != null && subscribed)
        {
            cachedAgent.OnHandoffCompleted -= HandleAgentHandoffCompleted;
            subscribed = false;
        }
        ActivateNow();
    }

    private void ActivateNow()
    {
        if (IsActivated) return;
        IsActivated = true;
        GroupHandoffStarted = false; // reset flag after synchronized entry completes
        OnActivated?.Invoke();
    }

    public void OnStopGameplay() { isPaused = true; }
    public void OnResumeGameplay() { isPaused = false; }
    #endregion

    #region Group Handoff (pivot-led sync)
    /// <summary>
    /// Called by this GO’s SpawnStageAgent right when the anchor begins its own handoff
    /// so followers can drag by the same delta in parallel.
    /// </summary>
    public void StartGroupHandoff(float anchorStartY, float anchorTargetY, float duration, AnimationCurve ease)
    {
        if (GroupHandoffStarted) return;
        GroupHandoffStarted = true;

        float deltaY = anchorTargetY - anchorStartY;

        foreach (var f in followers)
        {
            if (f == null) continue;
            if (!f.TryGetComponent<SpawnStageAgent>(out var agent)) continue;

            agent.BeginSynchronizedHandoffDelta(deltaY, duration, ease);
        }
    }
    #endregion

    #region Public API
    public void Configure(string key, float angularDegPerSec, float vSpeed)
    {
        anchorKey = key;
        angularSpeedDegPerSec = Mathf.Max(0f, angularDegPerSec);
        verticalSpeed = Mathf.Max(0f, vSpeed);

        if (!string.IsNullOrEmpty(key))
        {
            Registry[key] = this;
            AnchorRegistered?.Invoke(key, this);
        }
    }
    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(anchorKey) &&
            Registry.TryGetValue(anchorKey, out var me) && me == this)
        {
            Registry.Remove(anchorKey);
            AnchorUnregistered?.Invoke(anchorKey, this);
        }

        if (cachedAgent != null && subscribed)
        {
            cachedAgent.OnHandoffCompleted -= HandleAgentHandoffCompleted;
            subscribed = false;
        }
    }
    #endregion
}
