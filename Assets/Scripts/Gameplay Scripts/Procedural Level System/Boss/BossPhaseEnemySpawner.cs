using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossPhaseEnemySpawner : MonoBehaviour
{
    #region Singleton
    private static BossPhaseEnemySpawner instance;
    public static BossPhaseEnemySpawner Instance
    {
        get
        {
            if (instance == null)
                Debug.LogError("[BossPhaseEnemySpawner] No instance in scene.");
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[BossPhaseEnemySpawner] Multiple instances detected. Destroying extra.");
            Destroy(gameObject);
            return;
        }
        instance = this;
        if (!targetCamera) targetCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
    #endregion

    #region Serialized (defaults are optional)
    [Header("Defaults (optional)")]
    [SerializeField, Tooltip("Used if no prefab provided in requests.")]
    private GameObject defaultEnemyPrefab;

    [SerializeField, Tooltip("Used if no movement provided in requests.")]
    private MovementDefinition defaultMovement;

    [Header("Camera / Positioning")]
    [SerializeField, Tooltip("Camera to compute top Y and center X. If null, Camera.main is used.")]
    private Camera targetCamera;

    [Tooltip("Fallback top Y offset if caller doesn't provide one.")]
    private float topYOffset = 1f;
    #endregion

    #region Per-owner tracking
    private readonly Dictionary<Object, List<GameObject>> ownerSpawned = new Dictionary<Object, List<GameObject>>();
    #endregion

    #region Custom Wave API
    public struct SpawnRequest
    {
        public int laneIndex;                // 0..(lanes-1)
        public GameObject prefab;            // optional (falls back to defaultEnemyPrefab)
        public MovementDefinition movement;  // optional (falls back to defaultMovement)
    }

    /// <summary>
    /// Spawns a wave with per-unit prefab/movement across specific lanes.
    /// 'owner' is used for later cleanup via StopForOwner(owner, true).
    /// </summary>
    public void SpawnCustomWave(Object owner,
                                IList<SpawnRequest> requests,
                                int lanes,
                                float laneSpacing,
                                float? topYOffsetOverride = null)
    {
        Debug.LogWarning($"[EnemyInitializer] ASDASDASDASNo EnemyHealth or BossHealth on {name}. Nothing to initialize.");

        if (requests == null || requests.Count == 0) return;

        var cam = targetCamera ? targetCamera : Camera.main;
        if (!cam) { Debug.LogWarning("[BossPhaseEnemySpawner] No camera available."); return; }

        float topY = cam.orthographic
            ? cam.transform.position.y + cam.orthographicSize
            : cam.ViewportToWorldPoint(new Vector3(0f, 1f, Mathf.Abs(cam.transform.position.z))).y;

        float centerX = cam.transform.position.x;
        float yOffset = topYOffsetOverride.HasValue ? topYOffsetOverride.Value : this.topYOffset;

        for (int i = 0; i < requests.Count; i++)
        {
            var req = requests[i];
            int li = Mathf.Clamp(req.laneIndex, 0, Mathf.Max(0, lanes - 1));
            float centeredIndex = li - (lanes - 1) * 0.5f;   // even → ±0.5, ±1.5, ...
            float x = centerX + centeredIndex * laneSpacing;

            Vector3 pos = new Vector3(x, topY + yOffset, 0f);
            var prefab = req.prefab ? req.prefab : defaultEnemyPrefab;
            if (!prefab)
            {
                Debug.LogError("[BossPhaseEnemySpawner] Missing prefab for request entry.");
                continue;
            }

            GameObject go = Instantiate(prefab, pos, Quaternion.identity);

            InitializeSpawnedEnemy(go, pos);

            var movement = req.movement ? req.movement : defaultMovement;
            if (movement != null)
                movement.AttachTo(go, grid: null, gridCell: default, tags: null);

            if (!ownerSpawned.TryGetValue(owner, out var list))
            {
                list = new List<GameObject>(16);
                ownerSpawned[owner] = list;
            }
            list.Add(go);
        }
    }

    private void InitializeSpawnedEnemy(GameObject go, Vector3 pos)
    {
        if (!go) return;

        // If something has a stage agent by accident, just arm/resume immediately
        if (go.TryGetComponent(out SpawnStageAgent agent))
        {
            var acts = go.GetComponents<IStageActivatable>();
            for (int k = 0; k < acts.Length; k++) acts[k].ArmAtEntry(pos);
            for (int k = 0; k < acts.Length; k++) acts[k].ResumeMover();

            //bool paused = PauseManager.Instance != null && PauseManager.Instance.IsGameplayStopped;
            //if (paused)
            //{
            //    for (int k = 0; k < acts.Length; k++) acts[k].PauseMover();
            //}
            //else
            //{
            //    for (int k = 0; k < acts.Length; k++) acts[k].ResumeMover();
            //}
        }

        if (go.TryGetComponent(out EnemyInitializer initializer))
        {
            int levelIndex1Based = 1;
            if (LevelContextBinder.Instance != null)
            {
                levelIndex1Based = Mathf.Max(1, LevelContextBinder.Instance.CurrentLevelNumber1Based);
            }
            else
            {
                Debug.LogWarning("[BossPhaseEnemySpawner] LevelContextBinder.Instance is null. Using levelIndex1Based=1.");
            }

            initializer.InitializeFromSpawn(levelIndex1Based);
            return;
        }

        Debug.LogWarning($"[BossPhaseEnemySpawner] Spawned {go.name} has no EnemyInitializer. Health not initialized.");
    }

    /// <summary>Destroys everything spawned under a given owner.</summary>
    public void StopForOwner(Object owner, bool destroySpawned = true)
    {
        if (!owner) return;
        if (!destroySpawned) return;

        if (ownerSpawned.TryGetValue(owner, out var list))
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i]) Destroy(list[i]);
            list.Clear();
        }
    }
    #endregion
}
