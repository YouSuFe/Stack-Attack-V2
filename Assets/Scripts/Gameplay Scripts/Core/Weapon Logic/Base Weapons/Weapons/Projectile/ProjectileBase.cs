using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public abstract class ProjectileBase : MonoBehaviour, IDamageDealer, IProjectile, IStoppable
{
    [Header("Damage")]
    [SerializeField] private int damageAmount = 1;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetimeSeconds = 6f;

    [Header("Piercing")]
    [SerializeField] private int startingPiercing = 0;

    [Header("Hit Counting")]
    [Tooltip("How this projectile contributes to the hit-based charge bar.")]
    [SerializeField] private HitCountPolicy hitCountPolicy = HitCountPolicy.OncePerTargetPerProjectile;

    private float lifeTimer;
    private int remainingPiercing;
    private GameObject owner;

    private bool isStopped;

    // Used only when policy == OncePerTargetPerProjectile
    private readonly HashSet<int> countedTargets = new HashSet<int>();

    // ---------------------------
    // Optional: pooling support
    // ---------------------------

    // If you use the pooling service, it will call BindPool(...) once when the instance is created.
    // If you are not pooling, ReturnToPool() simply disables the object as a safe fallback.
    private ProjectilePoolService poolService;   // assigned once by the pool
    private ProjectileBase prefabKey;            // prefab identity for this instance (pool key)

    /// <summary>Bound by the pool when the instance is first created.</summary>
    public void BindPool(ProjectilePoolService service, ProjectileBase prefab)
    {
        poolService = service;
        prefabKey = prefab;
    }

    /// <summary>Called by the pool on every Get(). Reset per-shot state here.</summary>
    public virtual void OnSpawnFromPool()
    {
        lifeTimer = 0f;
        countedTargets.Clear();
        // If you ignore owner collisions or have other per-shot guards, re-apply them here.
    }

    /// <summary>Called by the pool on every Release(). Clean visuals here if needed.</summary>
    public virtual void OnReturnToPool()
    {
        // Example: trailRenderer?.Clear(); particleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }


    // ---- IDamageDealer ----
    public int DamageAmount => damageAmount;
    public GameObject Owner => owner != null ? owner : gameObject;

    // ---- IProjectile ----
    /// <summary>
    /// Backwards-compatible Initialize. Defaults to the serialized hitCountPolicy.
    /// </summary>
    public void Initialize(GameObject initOwner, int initDamage, int initPiercing)
    {
        Initialize(initOwner, initDamage, initPiercing, hitCountPolicy);
    }

    /// <summary>
    /// Extended Initialize allowing the spawner to set a policy per projectile.
    /// </summary>
    public virtual void Initialize(GameObject initOwner, int initDamage, int initPiercing, HitCountPolicy policy)
    {
        owner = initOwner;
        damageAmount = Mathf.Max(1, initDamage);
        startingPiercing = Mathf.Max(0, initPiercing);
        remainingPiercing = startingPiercing;

        hitCountPolicy = policy;
        countedTargets.Clear();
    }

    protected virtual void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        lifeTimer = 0f;
        remainingPiercing = startingPiercing;
        countedTargets.Clear();
    }

    protected virtual void Update()
    {
        if (isStopped) return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifetimeSeconds)
        {
            ReturnToPool();
            return;
        }

        TickMotion(Time.deltaTime);
    }

    protected abstract void TickMotion(float dt);

    protected virtual void OnEnable() { PauseManager.Instance?.Register(this); }
    protected virtual void OnDisable() { PauseManager.Instance?.Unregister(this); }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isStopped) return; // ignore collisions while paused

        // Don't hit our owner
        if ((owner != null && other.gameObject == owner) || other.CompareTag("Projectile") || other.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent<IDamageable>(out var target))
            return;

        // --- Apply damage ---
        target.TakeDamage(Mathf.Max(1, damageAmount), Owner);

        // --- Decide if this hit should count for the hit-based charge bar ---
        bool shouldCountHit = true;

        if (hitCountPolicy == HitCountPolicy.OncePerTargetPerProjectile)
        {
            // Use the damageable's root to avoid double-counting multi-collider enemies
            int targetId = TryGetRootId(target, other);
            shouldCountHit = countedTargets.Add(targetId);  // true only on first contact with this target
        }
        // else CountEveryEntry => every trigger counts

        if (shouldCountHit && owner != null && owner.CompareTag("Player"))
        {
            // Notify systems (e.g., SpecialSkillDriver) that the PLAYER landed a hit
            HitEventBus.RaisePlayerHit(target, owner);
        }

        // --- Handle piercing ---
        if (remainingPiercing > 0)
        {
            remainingPiercing--;
            // keep flying
        }
        else
        {
            ReturnToPool();
        }
    }

    public void OnStopGameplay()
    {
        isStopped = true;
    }

    public void OnResumeGameplay()
    {
        isStopped = false;
    }

    /// <summary>
    /// Prefer the ID of the root transform of the IDamageable (handles multi-collider targets).
    /// Fallback to the collider ID if no component context is available.
    /// </summary>
    private static int TryGetRootId(IDamageable damageable, Collider2D col)
    {
        if (damageable is Component comp && comp != null)
            return comp.transform.root.GetInstanceID();

        return col.GetInstanceID();
    }

    /// <summary>
    /// Return this instance to its pool (if any). Safe fallback disables the GameObject.
    /// </summary>
    protected void ReturnToPool()
    {
        if (poolService != null)
        {
            poolService.Despawn(this);
        }
        else
        {
            // If not pooled, disabling avoids GC churn and lets you still inspect state in playmode.
            // If you really want the old behavior, swap to Destroy(gameObject) here.
            gameObject.SetActive(false);
        }
    }
}
