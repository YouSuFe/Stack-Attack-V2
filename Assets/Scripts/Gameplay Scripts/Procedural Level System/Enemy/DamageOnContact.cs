using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deals damage to IDamageable targets on trigger contact.
/// - Pause-aware via IStoppable (stops dealing damage while paused)
/// - Per-target cooldown to avoid melting while overlapping
/// - LayerMask + optional tag filter to aim at the Player (or others)
/// - Implements IDamageDealer so damage source is tracked
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DamageOnContact : MonoBehaviour, IDamageDealer, IPausable
{
    #region Serialized
    [Header("Damage")]
    [SerializeField, Min(1)] private int damageAmount = 1;

    [Tooltip("If null, Owner defaults to this GameObject.")]
    [SerializeField] private GameObject owner;

    [Header("Target Filter")]
    [SerializeField, Tooltip("Only colliders on these layers are considered valid targets.")]
    private LayerMask targetLayers;

    [SerializeField, Tooltip("If set, target must also have this tag. Leave empty to ignore tag check.")]
    private string requiredTargetTag = "Player";

    [Header("Cadence")]
    [SerializeField, Tooltip("Cooldown per TARGET between contact hits (seconds).")]
    private float perTargetCooldown = 0.5f;

    [SerializeField, Tooltip("If true, deal damage only on enter. If false, also on stay respecting cooldown.")]
    private bool onlyOnEnter = false;

    #endregion

    #region State
    private readonly Dictionary<int, float> lastHitTimeByTargetId = new Dictionary<int, float>();
    private Collider2D triggerCol;
    private bool isPaused;
    #endregion

    #region IDamageDealer
    public int DamageAmount => damageAmount;
    public GameObject Owner => owner != null ? owner : gameObject;
    public void SetOwner(GameObject gameObject) => owner = gameObject;
    #endregion

    #region Unity
    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        triggerCol = GetComponent<Collider2D>();
        if (triggerCol != null && !triggerCol.isTrigger)
        {
            Debug.LogWarning($"[DamageOnContact] Collider on {name} should be Trigger. Setting isTrigger = true.");
            triggerCol.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        PauseManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        PauseManager.Instance?.Unregister(this);
        lastHitTimeByTargetId.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other, allowFirstHit: true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!onlyOnEnter) TryHit(other, allowFirstHit: false);
    }
    #endregion

    #region Core
    private void TryHit(Collider2D other, bool allowFirstHit)
    {
        if (isPaused) return;

        // Layer filter
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // Optional tag filter
        if (!string.IsNullOrEmpty(requiredTargetTag) && !other.CompareTag(requiredTargetTag)) return;

        // Acquire IDamageable (on self or parent)
        if (!other.TryGetComponent<IDamageable>(out var damageable))
            damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || !damageable.IsAlive) return;

        // Per-target cooldown using root instance id (stable across child colliders)
        int targetId = other.transform.root.GetInstanceID();
        float now = Time.time;

        if (!lastHitTimeByTargetId.TryGetValue(targetId, out float lastTime))
            lastTime = -999999f;

        float elapsed = now - lastTime;

        // On Enter we usually allow immediate hit; On Stay requires cooldown
        if (!allowFirstHit && elapsed < Mathf.Max(0f, perTargetCooldown))
            return;

        // Apply damage
        damageable.TakeDamage(Mathf.Max(1, damageAmount), Owner);
        lastHitTimeByTargetId[targetId] = now;
    }
    #endregion

    #region IStoppable
    public void OnStopGameplay() { isPaused = true; }
    public void OnResumeGameplay() { isPaused = false; }
    #endregion
}
