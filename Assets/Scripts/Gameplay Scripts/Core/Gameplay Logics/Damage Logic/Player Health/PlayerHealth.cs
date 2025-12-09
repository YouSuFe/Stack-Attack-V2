using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Hearts (no max cap)")]
    [SerializeField] private int startingHearts = 3;

    [Header("Invulnerability / Shield")]
    [SerializeField] private float invulnerabilityDurationSeconds = 4f;
    [SerializeField] private GameObject shieldVisualRoot;      // assign a child GameObject for the shield fx
    [SerializeField] private float shieldBlinkLeadSeconds = 3f; // last N seconds will blink
    [SerializeField] private float shieldBlinkFrequencyHz = 6f; // how fast it blinks near the end

    public int CurrentHearts { get; private set; }
    public bool IsAlive => CurrentHearts > 0;
    public bool IsInvulnerable { get; private set; }

    // Events
    public event Action<int, int, GameObject> OnDamaged;   // (currentHearts, damageAmount, source)
    public event Action<int, int> OnHealed;                // (currentHearts, healAmount)
    public event Action OnDied;
    public event Action<float> OnInvulnerabilityStarted;   // duration
    public event Action OnInvulnerabilityEnded;

    private Coroutine invulnerabilityCoroutine;

    private void Awake()
    {
        CurrentHearts = Mathf.Max(0, startingHearts);

        // Ensure trigger-based overlaps for this genre
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null) collider2D.isTrigger = true;

        if (shieldVisualRoot != null) shieldVisualRoot.SetActive(false);
    }

    // ===== IDamageable =====
    public void TakeDamage(int damageAmount, GameObject damageSource)
    {
        if (!IsAlive) return;
        if (IsInvulnerable) return;
        if (damageAmount <= 0) return;

        int previousHearts = CurrentHearts;
        CurrentHearts = Mathf.Max(0, CurrentHearts - damageAmount);

        Debug.Log($"[TakeDamage] Previous Health: {previousHearts}, Damage: {damageAmount}, Current Health: {CurrentHearts}");

        OnDamaged?.Invoke(CurrentHearts, damageAmount, damageSource);

        if (CurrentHearts <= 0)
        {
            Die();
            return;
        }

        StartInvulnerability();
    }

    // ===== Public API =====
    public void Heal(int healAmount)
    {
        if (!IsAlive) return;
        if (healAmount <= 0) return;

        CurrentHearts += healAmount; // no max cap
        OnHealed?.Invoke(CurrentHearts, healAmount);
    }

    public void AddHearts(int additionalHearts)
    {
        if (additionalHearts <= 0) return;
        CurrentHearts += additionalHearts; // no cap
        OnHealed?.Invoke(CurrentHearts, additionalHearts);
    }

    public void ForceInvulnerability(float durationSeconds)
    {
        if (durationSeconds <= 0f) return;

        if (invulnerabilityCoroutine != null)
            StopCoroutine(invulnerabilityCoroutine);

        invulnerabilityCoroutine = StartCoroutine(InvulnerabilityRoutine(durationSeconds));
    }

    // ===== Internals =====
    private void Die()
    {
        OnDied?.Invoke();
        // Disable gameplay here or notify GameManager via event subscribers.
        Debug.Log("[PlayerHealth] Player is dead.");
        gameObject.SetActive(false);
    }

    public void Revive()
    {
        // Put it here to prevent instant damage when revive
        IsInvulnerable = true;

        // Reactivate player if disabled
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        // Make player invulnerable for a short period after revive
        StartInvulnerability();

        // Restore health 
        CurrentHearts = startingHearts;

        // Trigger healing event for UI updates
        OnHealed?.Invoke(CurrentHearts, 3);

        Debug.Log("[PlayerHealth] Player revived with 3 hearts and temporary invulnerability.");
    }

    private void StartInvulnerability()
    {
        if (invulnerabilityCoroutine != null)
            StopCoroutine(invulnerabilityCoroutine);

        invulnerabilityCoroutine = StartCoroutine(InvulnerabilityRoutine(invulnerabilityDurationSeconds));
    }

    private IEnumerator InvulnerabilityRoutine(float duration)
    {
        IsInvulnerable = true;
        OnInvulnerabilityStarted?.Invoke(duration);

        if (shieldVisualRoot != null) shieldVisualRoot.SetActive(true);

        float timeRemaining = duration;
        float blinkStartAt = Mathf.Max(0f, duration - Mathf.Max(0f, shieldBlinkLeadSeconds));
        bool blinkingPhase = false;
        float blinkTimer = 0f;
        float blinkHalfPeriod = (shieldBlinkFrequencyHz > 0f) ? (0.5f / shieldBlinkFrequencyHz) : 0.1f;
        bool shieldVisible = true;

        while (timeRemaining > 0f && !PauseManager.Instance.IsGameplayStopped)
        {
            float dt = Time.deltaTime;
            timeRemaining -= dt;

            // Enter blinking phase when remaining time <= shieldBlinkLeadSeconds
            if (!blinkingPhase && timeRemaining <= shieldBlinkLeadSeconds)
            {
                blinkingPhase = true;
                blinkTimer = 0f;
            }

            if (shieldVisualRoot != null && blinkingPhase)
            {
                blinkTimer += dt;
                if (blinkTimer >= blinkHalfPeriod)
                {
                    blinkTimer = 0f;
                    shieldVisible = !shieldVisible;
                    shieldVisualRoot.SetActive(shieldVisible);
                }
            }

            yield return null;
        }

        if (shieldVisualRoot != null) shieldVisualRoot.SetActive(false);

        IsInvulnerable = false;
        OnInvulnerabilityEnded?.Invoke();
        invulnerabilityCoroutine = null;
    }
}
