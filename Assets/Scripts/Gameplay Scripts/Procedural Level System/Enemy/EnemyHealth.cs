using System;
using UnityEngine;

/// <summary>
/// Generic health component for regular enemies.
/// Implements IDamageable and IPausable, cooperates with PauseManager,
/// and is pool-friendly (optionally disables instead of destroying).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class EnemyHealth : MonoBehaviour, IDamageable
{
    #region Serialized
    [Header("Lifecycle")]
    [SerializeField, Tooltip("If true, Destroy on death; otherwise disable for pooling.")]
    private bool destroyOnDeath = true;

    [Header("FX (Optional)")]
    [SerializeField, Tooltip("Optional death VFX prefab to spawn at death.")]
    private GameObject deathEffect;

    [Header("Audio")]
    [SerializeField, Tooltip("Sound played when this enemy takes damage (non-lethal hit).")]
    private SoundData hitSound;

    [SerializeField, Tooltip("Sound played when this enemy dies.")]
    private SoundData deathSound;


    public int currentHealthValue =0;
    #endregion

    #region Private Fields
    private int maxHealth = 1;
    private int currentHealth = 1;
    private bool isAlive;
    #endregion

    #region Properties
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsAlive => isAlive; // IDamageable
    #endregion

    #region Events
    /// <summary>
    /// Fired when this enemy dies.
    /// </summary>
    public event Action<EnemyHealth> OnDied;

    /// <summary>
    /// Fired whenever health changes (including initialization and damage).
    /// Args: currentHealth, maxHealth.
    /// </summary>
    public event Action<int, int> OnHealthChanged;
    #endregion

    #region Initialization
    /// <summary>Called once at spawn by EnemyInitializer.</summary>
    public void InitializeHealth(int computedMaxHealth)
    {
        maxHealth = Mathf.Max(1, computedMaxHealth);
        currentHealthValue = currentHealth;
        currentHealth = maxHealth;
        isAlive = true;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    #endregion

    #region IDamageable
    /// <summary>
    /// Apply damage from a dealer. Damage is ignored if dead, or (optionally) while paused.
    /// </summary>
    public void TakeDamage(int damageAmount, GameObject damageSource)
    {
        if (!isAlive) return;

        int applied = Mathf.Max(1, Mathf.Abs(damageAmount));
        currentHealth = Mathf.Max(0, currentHealth - applied);
        currentHealthValue = currentHealth;
        Debug.Log($"[EnemyHealth] -{applied} from {(damageSource ? damageSource.name : "Unknown")} => {currentHealth}/{maxHealth}");

        // Play hit sound for this damage event (even if it kills the enemy)
        if (hitSound != null)
        {
            SoundUtils.PlayAtPosition(hitSound, transform.position);
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    #endregion

    #region Death
    private void Die()
    {
        if (!isAlive) return;
        isAlive = false;

        // Death sound
        if (deathSound != null)
        {
            SoundUtils.PlayAtPosition(deathSound, transform.position);
        }

        OnDied?.Invoke(this);

        if (deathEffect)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    #endregion
}
