using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Scene-level glue: listens to a BossStateController and, using a BossAddsProfile,
/// instructs BossPhaseEnemySpawner to spawn waves with mixed prefabs & movements.
/// </summary>
[DisallowMultipleComponent]
public class BossAddsOrchestrator : MonoBehaviour
{
    #region Serialized
    [Header("Bindings")]

    [SerializeField, Tooltip("Profile defining lanes, timing, and weighted prefab/movement choices.")]
    private BossAddsProfile profile;
    #endregion

    #region Private
    private BossStateController boss;
    private Coroutine waveLoop;
    #endregion

    #region Unity
    private void OnEnable()
    {
        HookBoss(boss); // safe if null
    }

    private void OnDisable()
    {
        UnhookBoss(boss);
        StopWaveLoop();
        BossPhaseEnemySpawner.Instance?.StopForOwner(this, destroySpawned: true);
    }
    #endregion

    #region Public API
    /// <summary>Call this from your sequencer after the boss is instantiated.</summary>
    public void HookBoss(BossStateController controller)
    {
        if (!controller) return;

        // Unhook previous (if any)
        UnhookBoss(boss);

        boss = controller;
        boss.OnFightStarted += HandleFightStarted;
        boss.OnStateChanged += HandleStateChanged;
    }

    public void UnhookBoss(BossStateController controller)
    {
        if (!controller) return;

        controller.OnFightStarted -= HandleFightStarted;
        controller.OnStateChanged -= HandleStateChanged;

        if (boss == controller) boss = null;
    }
    #endregion

    #region Handlers
    private void HandleFightStarted()
    {
        if (!profile)
        {
            Debug.LogWarning("[BossAddsOrchestrator] No profile assigned; not spawning adds.");
            return;
        }
        StartWaveLoop();
    }

    private void HandleStateChanged(BossStateController.BossState state)
    {
        if (state == BossStateController.BossState.Breaking ||
            state == BossStateController.BossState.Pinata ||
            state == BossStateController.BossState.End)
        {
            StopWaveLoop();
            BossPhaseEnemySpawner.Instance?.StopForOwner(this, destroySpawned: true);
        }
    }
    #endregion

    #region Wave Loop
    private void StartWaveLoop()
    {
        StopWaveLoop();
        waveLoop = StartCoroutine(WaveLoopRoutine());
    }

    private void StopWaveLoop()
    {
        if (waveLoop != null)
        {
            StopCoroutine(waveLoop);
            waveLoop = null;
        }
    }

    private IEnumerator WaveLoopRoutine()
    {
        var spawner = BossPhaseEnemySpawner.Instance;
        if (spawner == null) yield break;

        int lanes = Mathf.Max(1, profile.laneCount);
        float spacing = Mathf.Max(0.001f, profile.laneSpacing);

        while (true)
        {
            while (PauseManager.Instance != null && PauseManager.Instance.IsGameplayStopped)
                yield return null;

            // Wave size
            int minC = Mathf.Max(1, profile.waveCountRange.x);
            int maxC = Mathf.Max(minC, profile.waveCountRange.y);
            int count = Mathf.Clamp(Random.Range(minC, maxC + 1), 1, lanes);

            // Unique lanes
            int[] laneIndices = UniqueLaneIndices(count, lanes);

            // Build requests
            var requests = new List<BossPhaseEnemySpawner.SpawnRequest>(count);
            for (int i = 0; i < count; i++)
            {
                requests.Add(new BossPhaseEnemySpawner.SpawnRequest
                {
                    laneIndex = laneIndices[i],
                    prefab = PickWeightedPrefab(),
                    movement = PickWeightedMovement()
                });
            }

            // Spawn
            spawner.SpawnCustomWave(owner: this,
                                    requests: requests,
                                    lanes: lanes,
                                    laneSpacing: spacing,
                                    topYOffsetOverride: profile.topYOffset);

            // Wait next interval (+ jitter)
            float t = profile.waveInterval + ((profile.waveIntervalJitter > 0f) ? Random.Range(0f, profile.waveIntervalJitter) : 0f);
            if (t < 0.05f) t = 0.05f;
            yield return PauseAwareCoroutine.Delay(t);
        }
    }
    #endregion

    #region Weighted Picks
    private GameObject PickWeightedPrefab()
    {
        var list = profile.prefabs;
        if (list == null || list.Count == 0) return null;

        float sum = 0f;
        for (int i = 0; i < list.Count; i++) sum += Mathf.Max(0f, list[i].weight);
        if (sum <= 0f) return list[Random.Range(0, list.Count)].prefab;

        float r = Random.value * sum;
        float acc = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            acc += Mathf.Max(0f, list[i].weight);
            if (r <= acc) return list[i].prefab;
        }
        return list[^1].prefab;
    }

    private MovementDefinition PickWeightedMovement()
    {
        var list = profile.movements;
        if (list == null || list.Count == 0) return null;

        float sum = 0f;
        for (int i = 0; i < list.Count; i++) sum += Mathf.Max(0f, list[i].weight);
        if (sum <= 0f) return list[Random.Range(0, list.Count)].movement;

        float r = Random.value * sum;
        float acc = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            acc += Mathf.Max(0f, list[i].weight);
            if (r <= acc) return list[i].movement;
        }
        return list[^1].movement;
    }
    #endregion

    #region Utils
    private static int[] UniqueLaneIndices(int count, int lanes)
    {
        int[] pool = new int[lanes];
        for (int i = 0; i < lanes; i++) pool[i] = i;

        for (int i = 0; i < count; i++)
        {
            int j = Random.Range(i, lanes);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int[] result = new int[count];
        for (int i = 0; i < count; i++) result[i] = pool[i];
        return result;
    }
    #endregion
}
