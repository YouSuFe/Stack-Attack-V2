using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpawnStageAgent))]
public class BossHealth : MonoBehaviour, IDamageable, IPausable
{
    #region Serialized
    [SerializeField, Tooltip("Maximum health of the boss.")]
    private int maxHealth = 100;

    [SerializeField, Tooltip("If true, boss starts invulnerable until controller enables damage.")]
    private bool startInvulnerable = true;

    [SerializeField, Tooltip("Clamp minimum damage per hit (0 = no clamp).")]
    private int minDamageClamp = 0;

    [SerializeField, Tooltip("If true, ignore damage while paused.")]
    private bool ignoreDamageWhenPaused = true;

    [Header("Audio")]
    [SerializeField, Tooltip("Sound played when the boss takes damage.")]
    private SoundData hitSound;

    [SerializeField, Tooltip("Sound played when the boss is broken (dies).")]
    private SoundData deadSound;
    #endregion

    #region Private
    private int currentHealth;
    private bool isAlive = true;
    private bool canTakeDamage;
    private bool isPaused;

    private SpawnStageAgent agent;
    private SegmentObject segmentObject; // optional bind from sequencer
    #endregion

    #region Actions
    public event Action<int, int> OnDamaged; // (current, max)
    public event Action OnBroken;
    #endregion

    #region Properties
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    // IDamageable
    public bool IsAlive => isAlive;
    #endregion

    #region Unity
    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        TryGetComponent(out agent);
        canTakeDamage = !startInvulnerable;
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
        TryGetComponent(out segmentObject);
        OnDamaged?.Invoke(currentHealth, maxHealth);
    }
    #endregion

    #region Initialization (for EnemyInitializer)
    public void InitializeMaxHealth(int newMax, bool resetCurrent)
    {
        maxHealth = Mathf.Max(1, newMax);
        if (resetCurrent || currentHealth > maxHealth)
            currentHealth = maxHealth;

        OnDamaged?.Invoke(currentHealth, maxHealth);
    }
    #endregion

    #region Boss Controller Integration
    public void AllowDamage(bool allow) => canTakeDamage = allow && isAlive;
    public void BindSegmentObject(SegmentObject so) => segmentObject = so;
    #endregion

    #region IDamageable
    public void TakeDamage(int damageAmount, GameObject damageSource)
    {
        if (!isAlive) return;
        if (!canTakeDamage) return;
        if (ignoreDamageWhenPaused && isPaused) return;

        int dmg = Mathf.Abs(damageAmount);
        if (minDamageClamp > 0 && dmg < minDamageClamp) dmg = minDamageClamp;

        currentHealth = Mathf.Max(0, currentHealth - dmg);

        // Hit sound
        if (hitSound != null)
        {
            SoundUtils.PlayAtPosition(hitSound, transform.position);
        }

        OnDamaged?.Invoke(currentHealth, maxHealth);

        if (currentHealth == 0) BreakBoss();
    }
    #endregion

    #region Break
    private void BreakBoss()
    {
        if (!isAlive) return;
        isAlive = false;
        canTakeDamage = false;

        // Break/death sound
        if (deadSound != null)
        {
            SoundUtils.PlayAtPosition(deadSound, transform.position);
        }

        OnBroken?.Invoke();
    }
    #endregion

    #region IPausable
    public void OnStopGameplay() { isPaused = true; }
    public void OnResumeGameplay() { isPaused = false; }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Take 10 Damage")]
    private void DebugDamage10() => TakeDamage(10, null);
#endif
}
