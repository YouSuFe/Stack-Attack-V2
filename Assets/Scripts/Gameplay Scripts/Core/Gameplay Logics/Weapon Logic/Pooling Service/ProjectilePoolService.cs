using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Prefab-keyed pooling for ProjectileBase using Unity's ObjectPool.
/// - Pools are created lazily on first use or on EnsurePool().
/// - Prewarm is opt-in (call EnsurePool for the specific weapon you equip).
/// - No lambdas; explicit callback methods.
/// </summary>
public class ProjectilePoolService : MonoBehaviour
{
    private class PoolCallbacks
    {
        private readonly ProjectilePoolService service;
        private readonly ProjectileBase prefab;
        private readonly Transform poolRoot;

        public ObjectPool<ProjectileBase> pool;

        public PoolCallbacks(ProjectilePoolService service, ProjectileBase prefab, Transform poolRoot)
        {
            this.service = service;
            this.prefab = prefab;
            this.poolRoot = poolRoot;

            pool = new ObjectPool<ProjectileBase>(
                CreateInstance,
                OnGet,
                OnRelease,
                OnDestroyInstance,
                defaultCapacity: 32,   // capacity hint for the inactive stack
                maxSize: 1024,         // max INACTIVE items retained for THIS prefab
                collectionCheck: Application.isEditor
            );
        }

        private ProjectileBase CreateInstance()
        {
            GameObject gameObject = Object.Instantiate(prefab.gameObject, poolRoot);
            ProjectileBase projectile = gameObject.GetComponent<ProjectileBase>();
            projectile.BindPool(service, prefab);               // remember service + prefab key
            gameObject.SetActive(false);
            service.prefabByInstance[projectile] = prefab;      // reverse map for Release
            return projectile;
        }

        private void OnGet(ProjectileBase projectile)
        {
            projectile.transform.SetParent(poolRoot, false);
            projectile.gameObject.SetActive(true);
            service.MarkActive(projectile);
        }

        private void OnRelease(ProjectileBase projectile)
        {
            projectile.OnReturnToPool();                        // cleanup visuals if any
            projectile.transform.SetParent(poolRoot, false);
            projectile.gameObject.SetActive(false);
            service.MarkInactive(projectile);
        }

        private void OnDestroyInstance(ProjectileBase projectile)
        {
            if (projectile != null) Object.Destroy(projectile.gameObject);
        }
    }

    private readonly Dictionary<ProjectileBase, PoolCallbacks> poolsByPrefab = new Dictionary<ProjectileBase, PoolCallbacks>();
    internal readonly Dictionary<ProjectileBase, ProjectileBase> prefabByInstance = new Dictionary<ProjectileBase, ProjectileBase>();

    // Track currently checked-out (active) instances
    private readonly HashSet<ProjectileBase> activeInstances = new HashSet<ProjectileBase>();

    private Transform poolRoot;

    private void Awake()
    {
        poolRoot = new GameObject("ProjectilePools").transform;
        poolRoot.SetParent(transform, false);
    }

    private void MarkActive(ProjectileBase instance)
    {
        if (instance != null) activeInstances.Add(instance);
    }

    private void MarkInactive(ProjectileBase instance)
    {
        if (instance != null) activeInstances.Remove(instance);
    }

    /// <summary>
    /// Create the pool for a prefab if missing and optionally prewarm a count.
    /// Call this when a weapon is EQUIPPED (lazy, weapon-specific).
    /// </summary>
    public void EnsurePool(ProjectileBase prefab, int prewarmCount = 0)
    {
        if (prefab == null) return;

        PoolCallbacks callbacks;
        if (!poolsByPrefab.TryGetValue(prefab, out callbacks))
        {
            callbacks = new PoolCallbacks(this, prefab, poolRoot);
            poolsByPrefab[prefab] = callbacks;
        }

        if (prewarmCount <= 0) return;

        // Create distinct instances by keeping them "checked out" first
        var temps = new List<ProjectileBase>(prewarmCount);
        for (int i = 0; i < prewarmCount; i++)
            temps.Add(callbacks.pool.Get());

        // Return them all; now the pool has 'prewarmCount' inactive items
        for (int i = 0; i < temps.Count; i++)
            callbacks.pool.Release(temps[i]);
    }

    /// <summary>
    /// Spawn an instance of the prefab at position/rotation. Creates the pool on demand.
    /// </summary>
    public ProjectileBase Spawn(ProjectileBase prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        PoolCallbacks callbacks;
        if (!poolsByPrefab.TryGetValue(prefab, out callbacks))
        {
            callbacks = new PoolCallbacks(this, prefab, poolRoot);
            poolsByPrefab[prefab] = callbacks;
        }

        ProjectileBase instance = callbacks.pool.Get();

        // Important: parent first (optional), then set pose, then OnSpawnFromPool
        if (parent != null)
            instance.transform.SetParent(parent, false);

        instance.transform.SetPositionAndRotation(position, rotation);

        // Now it’s safe for projectiles (e.g., missiles) to cache startPosition/forward
        instance.OnSpawnFromPool();

        return instance;
    }

    /// <summary>
    /// Return an instance to its prefab's pool.
    /// </summary>
    public void Despawn(ProjectileBase instance)
    {
        if (instance == null) return;

        if (!activeInstances.Contains(instance))
            return;

        ProjectileBase prefabKey;
        if (!prefabByInstance.TryGetValue(instance, out prefabKey))
        {
            instance.gameObject.SetActive(false); // safety
            activeInstances.Remove(instance); // safety
            return;
        }

        PoolCallbacks callbacks;
        if (!poolsByPrefab.TryGetValue(prefabKey, out callbacks))
        {
            instance.gameObject.SetActive(false); // safety
            activeInstances.Remove(instance); // safety
            return;
        }

        callbacks.pool.Release(instance);
    }

    public bool TryGetPrefabForInstance(ProjectileBase instance, out ProjectileBase prefab)
    {
        return prefabByInstance.TryGetValue(instance, out prefab);
    }

    public ProjectileBase SpawnLike(ProjectileBase sourceInstance, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (sourceInstance == null) return null;

        if (!prefabByInstance.TryGetValue(sourceInstance, out var prefabKey) || prefabKey == null)
        {
            // Safety fallback: treat the current object as prefab-like (rare)
            prefabKey = sourceInstance;
        }

        // Reuse the normal pooled Spawn(prefab, ...), which calls OnSpawnFromPool internally
        var clone = Spawn(prefabKey, position, rotation, parent);
        return clone;
    }

    /// <summary>
    /// Despawn all currently active projectiles across all pools.
    /// Safe to call on level fail/success/menu transitions.
    /// </summary>
    public void DespawnAllActive()
    {
        if (activeInstances.Count == 0) return;

        // Copy first to avoid modifying while iterating
        var temps = new List<ProjectileBase>(activeInstances.Count);
        foreach (var inst in activeInstances)
            if (inst != null) temps.Add(inst);

        for (int i = 0; i < temps.Count; i++)
            Despawn(temps[i]);

        temps.Clear();
    }
}

