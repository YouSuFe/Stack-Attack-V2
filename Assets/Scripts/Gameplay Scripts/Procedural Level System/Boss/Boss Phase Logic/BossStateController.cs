using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpawnStageAgent))]
[RequireComponent(typeof(BossHealth))]
public class BossStateController : MonoBehaviour
{
    public enum BossState { Spawning, Arriving, Fight, Breaking, Pinata, Done }

    #region Orbiters
    [Header("Orbiters")]
    [SerializeField, Tooltip("Prefab that has a HexOrbitFollower on it (or will be added at runtime).")]
    private GameObject orbitingMinionPrefab;

    [SerializeField, Tooltip("How many orbiters to spawn when Fight starts.")]
    private int orbiters = 6;

    [SerializeField, Tooltip("Radius of the hex path (center to corner).")]
    private float orbitRadius = 3.5f;

    [SerializeField, Tooltip("Units/sec along each straight edge of the hex.")]
    private float edgeSpeed = 4.0f;

    [SerializeField, Tooltip("Pause seconds at each hex corner to emphasize the 'turn'.")]
    private float cornerHold = 0.05f;

    [SerializeField, Range(0f, 1f), Tooltip("Random jitter added to each orbiter's phase offset.")]
    private float phaseJitter = 0.08f;

    [SerializeField, Tooltip("Delay between spawning orbiters at Fight start.")]
    private float spawnStagger = 0.08f;

    [SerializeField, Tooltip("Rotate hex shape by degrees. 0 = flat top (with the fixed follower).")]
    private float rotationOffsetDeg = 0f;

    [SerializeField, Tooltip("If true, orbit direction is clockwise (screen coords).")]
    private bool clockwise = true;
    #endregion

    #region Pinata
    [Header("Pinata")]
    [SerializeField, Tooltip("Pinata scoring meter (attach on the boss GO).")]
    private PinataMeter pinataMeter;

    [SerializeField, Tooltip("Seconds to wait after boss breaks before starting pinata (defeat pause).")]
    private float breakToPinataDelay = 3.0f;
    #endregion

    #region Private
    private SpawnStageAgent agent;
    private BossHealth health;

    private readonly List<GameObject> spawnedOrbiters = new List<GameObject>(16);
    private IStageActivatable[] bossMoversCache;

    private Coroutine stateRoutine;
    private BossState state = BossState.Spawning;

    private SegmentObject segmentObject; // optional late-bound
    #endregion

    #region Actions
    public event Action<BossState> OnStateChanged;
    public event Action OnFightStarted;
    public event Action OnPinataStarted;
    public event Action OnPinataEnded;
    #endregion

    #region Unity
    private void Awake()
    {
        TryGetComponent(out agent);
        TryGetComponent(out health);
        if (!pinataMeter) TryGetComponent(out pinataMeter);
        state = BossState.Spawning;
    }

    private void OnEnable()
    {
        if (!agent) TryGetComponent(out agent);
        if (agent) agent.OnHandoffCompleted += HandleHandoffCompleted;

        if (!health) TryGetComponent(out health);
        if (health) health.OnBroken += HandleBossBroken;
    }

    private void Start()
    {
        TryGetComponent(out segmentObject); // sequencer may attach at runtime
    }

    private void OnDisable()
    {
        if (agent) agent.OnHandoffCompleted -= HandleHandoffCompleted;
        if (health) health.OnBroken -= HandleBossBroken;
    }
    #endregion

    #region Public (late bind)
    public void BindSegmentObject(SegmentObject so) => segmentObject = so;
    #endregion

    #region Flow
    private void HandleHandoffCompleted()
    {
        if (state != BossState.Spawning && state != BossState.Arriving) return;

        ChangeState(BossState.Arriving);

        // Lock movers so boss stays in place
        bossMoversCache ??= GetComponentsInChildren<IStageActivatable>(includeInactive: true);
        for (int i = 0; i < bossMoversCache.Length; i++)
            bossMoversCache[i].PauseMover();

        StartFight();
    }

    private void StartFight()
    {
        ChangeState(BossState.Fight);
        health?.AllowDamage(true);

        // Orbiters
        if (orbitingMinionPrefab && orbiters > 0)
            StartCoroutine(SpawnOrbitersRoutine());

        OnFightStarted?.Invoke();

        // === TEMP TEST ===
        // Automatically go to pinata after 5 seconds (for testing only)
        StartCoroutine(TestGoToPinataAfterDelay());
        // =================
    }

    // === TEMP TEST ===
    private IEnumerator TestGoToPinataAfterDelay()
    {
        yield return PauseAwareCoroutine.Delay(5f);
        Debug.Log("[BossStateController] TEST: Auto switching to Pinata state.");
        HandleBossBroken(); // simulate boss defeat
    }
    // =================

    private void HandleBossBroken()
    {
        if (state != BossState.Fight) return;
        ChangeState(BossState.Breaking);

        StopAndClearOrbiters();
        health?.AllowDamage(false);

        if (stateRoutine != null) StopCoroutine(stateRoutine);
        stateRoutine = StartCoroutine(BreakToPinataRoutine());
    }

    private IEnumerator BreakToPinataRoutine()
    {
        if (breakToPinataDelay > 0f)
            yield return PauseAwareCoroutine.Delay(breakToPinataDelay);

        if (PinataDirector.Instance != null && pinataMeter != null)
            PinataDirector.Instance.BeginPinataFor(this, pinataMeter);

        ChangeState(BossState.Pinata);
        OnPinataStarted?.Invoke();
    }

    public void EndPinata()
    {
        if (state != BossState.Pinata) return;
        PinataDirector.Instance?.EndPinata();
        ChangeState(BossState.Done);
        OnPinataEnded?.Invoke();
        // Optionally Destroy(gameObject) or notify sequencer here.
    }
    #endregion

    #region Orbiters
    private IEnumerator SpawnOrbitersRoutine()
    {
        for (int i = 0; i < orbiters; i++)
        {
            GameObject go = Instantiate(orbitingMinionPrefab, transform.position, Quaternion.identity);
            spawnedOrbiters.Add(go);

            if (!go.TryGetComponent(out HexOrbitFollower follower))
                follower = go.AddComponent<HexOrbitFollower>();

            float basePhase = (float)i / Mathf.Max(1, orbiters);
            float jitter = (phaseJitter > 0f) ? UnityEngine.Random.Range(-phaseJitter, phaseJitter) : 0f;
            float phase = Mathf.Repeat(basePhase + jitter, 1f);

            follower.Initialize(center: transform,
                                radius: orbitRadius,
                                edgeSpeed: edgeSpeed,
                                cornerHold: cornerHold,
                                phaseOffset: phase,
                                rotationOffsetDeg: rotationOffsetDeg, // 0 = flat top with the fixed follower
                                clockwise: clockwise);

            if (spawnStagger > 0f)
                yield return PauseAwareCoroutine.Delay(spawnStagger);
        }
    }

    public void StopAndClearOrbiters()
    {
        for (int i = 0; i < spawnedOrbiters.Count; i++)
            if (spawnedOrbiters[i]) Destroy(spawnedOrbiters[i]);
        spawnedOrbiters.Clear();
    }
    #endregion

    #region Helpers
    private void ChangeState(BossState next)
    {
        if (state == next) return;
        state = next;
        OnStateChanged?.Invoke(state);
    }
    #endregion
}
