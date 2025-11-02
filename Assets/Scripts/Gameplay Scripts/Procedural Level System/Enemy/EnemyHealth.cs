using System;
using UnityEngine;

/// <summary>
/// Generic health component for regular enemies.
/// Implements IDamageable and IStoppable, cooperates with PauseManager,
/// and is pool-friendly (optionally disables instead of destroying).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class EnemyHealth : MonoBehaviour, IDamageable, IStoppable
{
    #region Serialized
    [Header("Lifecycle")]
    [SerializeField, Tooltip("If true, Destroy on death; otherwise disable for pooling.")]
    private bool destroyOnDeath = true;

    [Header("FX (Optional)")]
    [SerializeField, Tooltip("Optional death VFX prefab to spawn at death.")]
    private GameObject deathEffect;

    [Header("Damage Gates")]
    [SerializeField, Tooltip("If true, ignores damage while paused.")]
    private bool ignoreDamageWhenPaused = true;
    #endregion

    #region Private Fields
    private int maxHealth = 1;
    private int currentHealth = 1;
    private bool isAlive;
    private bool isPaused;
    #endregion

    #region Properties
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsAlive => isAlive; // IDamageable
    #endregion

    public event Action<EnemyHealth> OnDied;

    #region Unity
    private void OnEnable()
    {
        PauseManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        PauseManager.Instance?.Unregister(this);
    }
    #endregion

    #region Initialization
    /// <summary>Called once at spawn by EnemyInitializer.</summary>
    public void InitializeHealth(int computedMaxHealth)
    {
        maxHealth = Mathf.Max(1, computedMaxHealth);
        currentHealth = maxHealth;
        isAlive = true;
    }
    #endregion

    #region IDamageable
    /// <summary>Apply damage from a dealer. Damage is ignored if dead, or (optionally) while paused.</summary>
    public void TakeDamage(int damageAmount, GameObject damageSource)
    {
        if (!isAlive) return;
        if (ignoreDamageWhenPaused && isPaused) return;

        int applied = Mathf.Max(1, Mathf.Abs(damageAmount));
        currentHealth -= applied;

        Debug.Log($"[EnemyHealth] -{applied} from {(damageSource ? damageSource.name : "Unknown")} => {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }
    #endregion

    #region Death
    private void Die()
    {
        if (!isAlive) return;
        isAlive = false;

        OnDied?.Invoke(this);

        if (deathEffect) Instantiate(deathEffect, transform.position, Quaternion.identity);

        if (destroyOnDeath) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
    #endregion

    #region IStoppable (Pause)
    public void OnStopGameplay() { isPaused = true; }
    public void OnResumeGameplay() { isPaused = false; }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Apply 1 Damage")]
    private void DebugDamage() => TakeDamage(1, null);
#endif
}
