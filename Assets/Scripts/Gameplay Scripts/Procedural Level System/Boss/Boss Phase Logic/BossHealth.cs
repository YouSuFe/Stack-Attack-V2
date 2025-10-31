using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpawnStageAgent))]
public class BossHealth : MonoBehaviour
{
    #region Serialized
    [SerializeField, Tooltip("Maximum health of the boss.")]
    private int maxHealth = 100;

    [SerializeField, Tooltip("If true, damage is ignored until the controller enables it (after arrival).")]
    private bool startInvulnerable = true;

    [SerializeField, Tooltip("Clamp minimum damage per hit (0 = no clamp).")]
    private int minDamageClamp = 0;
    #endregion

    #region Private
    private int currentHealth;
    private bool isAlive = true;
    private bool canTakeDamage;

    private SpawnStageAgent agent;
    private SegmentObject segmentObject; // may be injected at runtime
    #endregion

    #region Actions
    public event Action<int, int> OnDamaged; // (current, max)
    public event Action OnBroken;
    #endregion

    #region Properties
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool CanTakeDamage => canTakeDamage && isAlive;
    #endregion

    #region Unity
    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        TryGetComponent(out agent);
        canTakeDamage = !startInvulnerable;
    }

    private void Start()
    {
        TryGetComponent(out segmentObject);
    }
    #endregion

    #region Public API
    public void AllowDamage(bool allow) => canTakeDamage = allow && isAlive;

    public void BindSegmentObject(SegmentObject so) => segmentObject = so;

    public void ApplyDamage(int amount)
    {
        if (!isAlive || !CanTakeDamage) return;

        int dmg = Mathf.Abs(amount);
        if (minDamageClamp > 0 && dmg < minDamageClamp) dmg = minDamageClamp;

        currentHealth = Mathf.Max(0, currentHealth - dmg);
        OnDamaged?.Invoke(currentHealth, maxHealth);

        if (currentHealth == 0) BreakBoss();
    }

    public void Heal(int amount)
    {
        if (!isAlive) return;
        currentHealth = Mathf.Clamp(currentHealth + Mathf.Abs(amount), 0, maxHealth);
        OnDamaged?.Invoke(currentHealth, maxHealth);
    }
    #endregion

    #region Break
    private void BreakBoss()
    {
        if (!isAlive) return;
        isAlive = false;
        canTakeDamage = false;
        OnBroken?.Invoke();
    }
    #endregion
}