// TestEnemy.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Very basic test enemy for validating damage, invulnerability, and instant-kill.
/// Moves toward the player and deals contact damage on trigger overlap.
/// Also takes damage from your projectiles (IDamageable).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TestEnemy : MonoBehaviour, IDamageable, IDamageDealer, IStoppable
{
    [Header("Stats")]
    [SerializeField] private int health = 3;
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Contact Damage")]
    [Tooltip("Seconds between successive hits to the SAME target while staying in contact.")]
    [SerializeField] private float contactDamageCooldownSeconds = 0.5f;
    [Tooltip("If true, only damage objects tagged 'Player'. If false, damage any IDamageable.")]
    [SerializeField] private bool onlyHitPlayerTag = true;

    [Header("Visuals")]
    [SerializeField] private GameObject deathEffect; // optional

    private Transform playerTarget;
    private Collider2D enemyCollider2D;
    private readonly Dictionary<int, float> lastHitTimeByTargetId = new Dictionary<int, float>();

    private bool isStopped;

    // ---- IDamageDealer ----
    public int DamageAmount => contactDamage;
    public GameObject Owner => gameObject;

    // Convenience
    public bool IsAlive => health > 0;

    private void Awake()
    {
        enemyCollider2D = GetComponent<Collider2D>();
        enemyCollider2D.isTrigger = true; // using trigger-based contact damage
    }

    private void OnEnable()
    {
        PauseManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        PauseManager.Instance?.Unregister(this);
    }

    private void Start()
    {
        // Find the player by tag if not manually assigned
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTarget = player.transform;
    }

    private void Update()
    {
        if (!IsAlive || playerTarget == null || isStopped) return;
        Debug.Log(isStopped + " ----- " + name);
        // Simple chase toward player
        Vector3 current = transform.position;
        Vector3 target = playerTarget.position;
        Vector3 direction = (target - current).normalized;

        transform.position += direction * moveSpeed * Time.deltaTime;
    }

    // ---- IDamageable ----
    public void TakeDamage(int damageAmount, GameObject damageSource = null)
    {
        if (!IsAlive) return;

        int applied = Mathf.Max(1, damageAmount);
        health -= applied;

        Debug.Log($"[TestEnemy] Took {applied} dmg from {(damageSource != null ? damageSource.name : "Unknown")} | HP: {health}");

        if (health <= 0)
        {
            Die(damageSource);
        }
    }

    public void TakeInstantKill(bool ignoreInvulnerability = true)
    {
        if (!IsAlive) return;
        health = 0;
        Die(null);
    }

    private void Die(GameObject killer)
    {
        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        Debug.Log($"{name} is killed by {(killer != null ? killer.name : "Unknown")}");
        Destroy(gameObject);
    }

    // ---- Contact damage to player (or any IDamageable if allowed) ----
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDealContactDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDealContactDamage(other);
    }

    private void TryDealContactDamage(Collider2D other)
    {
        if (!IsAlive || isStopped) return;

        // Optional: restrict to Player tag
        if (onlyHitPlayerTag && !other.CompareTag("Player"))
            return;

        // Must have an IDamageable to hurt
        if (!other.TryGetComponent<IDamageable>(out var damageable))
            return;

        // Throttle per-target using instance ID
        int targetId = other.transform.root.GetInstanceID();
        float now = Time.time;

        if (!lastHitTimeByTargetId.TryGetValue(targetId, out float lastTime))
            lastTime = -999f;

        if (now - lastTime >= contactDamageCooldownSeconds)
        {
            lastHitTimeByTargetId[targetId] = now;

            // Deal damage as an IDamageDealer (Owner = this enemy)
            damageable.TakeDamage(Mathf.Max(1, contactDamage), Owner);
            Debug.Log($"[TestEnemy] Dealt {contactDamage} contact dmg to {other.name}");
        }
    }

    // ------------------------
    // IStoppable
    // ------------------------

    public void OnStopGameplay()
    {
        isStopped = true;
    }

    public void OnResumeGameplay()
    {
        isStopped = false;
    }
}
