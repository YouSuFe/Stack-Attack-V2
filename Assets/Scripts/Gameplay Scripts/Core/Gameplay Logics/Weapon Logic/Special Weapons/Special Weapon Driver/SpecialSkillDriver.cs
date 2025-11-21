using System;
using UnityEngine;

/// <summary>
/// Owns the special-skill state (charge bar, activation window).
/// Rules:
/// - Charge fills on player hits (via HitEventBus).
/// - No cooldown.
/// - Fires only on input RELEASE and only when in combat and bar full.
/// - Empties bar on fire.
/// - Skill visuals run for ActiveDurationSeconds.
/// </summary>
public class SpecialSkillDriver : MonoBehaviour, IPausable
{
    [Header("Definition & Mounts")]
    [SerializeField] private SpecialSkillDefinitionSO specialDefinition;
    [SerializeField] private Transform skillOrigin;

    [Header("Runtime State (debug)")]
    [SerializeField] private int currentCharge;
    [SerializeField] private bool isActive;
    [SerializeField] private bool isInCombat;

    private ISpecialSkill activeSkill;
    private float activeTimer;
    private GameObject owner;

    private bool isPaused;

    // UI events
    public Action<int, int> OnChargeChanged;  // (current, required)
    public Action OnSkillActivated;
    public Action OnSkillEnded;

    private void Awake()
    {
        owner = gameObject;

        if (specialDefinition == null)
        {
            Debug.LogWarning("SpecialSkillDriver: 'specialDefinition' not assigned.");
            return;
        }

        switch (specialDefinition.SpecialSkillType)
        {
            case SpecialSkillType.Laser:
                activeSkill = new LaserSkill(skillOrigin, specialDefinition);
                break;
            default:
                Debug.LogError("SpecialSkillDriver: Unhandled SpecialSkillType.");
                break;
        }

        activeSkill?.Initialize(specialDefinition, owner);
        NotifyChargeChanged();
    }

    private void OnEnable()
    {
        HitEventBus.OnPlayerHit += HandlePlayerHit;
    }

    private void OnDisable()
    {
        HitEventBus.OnPlayerHit -= HandlePlayerHit;

        if (isActive)
        {
            isActive = false;
            activeSkill?.Stop();
            OnSkillEnded?.Invoke();
        }
    }

    private void Update()
    {
        if (!isActive || isPaused) return;

        float dt = Time.deltaTime;
        activeTimer -= dt;
        activeSkill?.TickActive(dt);

        if (activeTimer <= 0f)
            EndSkill();
    }

    // -------- Public API --------

    /// <summary>Game flow toggles fight/treasure phases.</summary>
    public void SetIsInCombat(bool value)
    {
        isInCombat = value;
    }

    /// <summary>
    /// Call this ONLY on input RELEASE. If in combat and bar full, fires the skill.
    /// </summary>
    public void NotifyShootInputReleased()
    {
        if (specialDefinition == null || isActive) return;
        if (!isInCombat) return;
        if (currentCharge < specialDefinition.RequiredCharge) return;

        if (activeSkill != null && activeSkill.TryActivate())
        {
            if (specialDefinition.FireSound != null)
                SoundUtils.Play2D(specialDefinition.FireSound);

            isActive = true;
            activeTimer = specialDefinition.ActiveDurationSeconds;
            SetCharge(0); // empty bar
            OnSkillActivated?.Invoke();
        }
    }

    // -------- Internals --------

    private void HandlePlayerHit(IDamageable target, GameObject dealerOwner)
    {
        if (dealerOwner != owner || specialDefinition == null) return;

        if (currentCharge < specialDefinition.RequiredCharge)
        {
            SetCharge(Mathf.Min(currentCharge + 1, specialDefinition.RequiredCharge));
        }
    }

    private void EndSkill()
    {
        isActive = false;
        activeSkill?.Stop();
        OnSkillEnded?.Invoke();
    }

    private void SetCharge(int value)
    {
        currentCharge = value;
        NotifyChargeChanged();
    }

    public void FillChargeToMax()
    {
        if (specialDefinition == null) return;
        // Set to required charge and notify UI
        currentCharge = specialDefinition.RequiredCharge;
        NotifyChargeChanged();
    }

    private void NotifyChargeChanged()
    {
        OnChargeChanged?.Invoke(currentCharge, specialDefinition != null ? specialDefinition.RequiredCharge : 1);
    }

    public void OnStopGameplay()
    {
        isPaused = true;
    }

    public void OnResumeGameplay()
    {
        isPaused = false;
    }
}
