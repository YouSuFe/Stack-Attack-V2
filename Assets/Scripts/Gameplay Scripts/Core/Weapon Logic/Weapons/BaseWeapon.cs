using System.Collections;
using UnityEngine;

public abstract class BaseWeapon : MonoBehaviour, IWeapon
{
    [Header("Definition (assign at runtime or Inspector for test)")]
    [SerializeField] private WeaponDefinitionSO weaponDefinition;

    [Header("Runtime Stats")]
    [SerializeField] private float fireRatePerSecond = 2f;
    [SerializeField] private int projectileAmount = 1;
    [SerializeField] private int piercing = 0;
    [SerializeField] private bool autoFireEnabled = true;

    [Header("State")]
    [SerializeField] private bool canAttack = true;

    private GameObject owner;
    private float nextFireTimeSeconds;
    private Coroutine autoFireCoroutine;

    public WeaponType WeaponType => weaponDefinition != null ? weaponDefinition.WeaponType : WeaponType.Basic;
    public bool CanAutoFire => autoFireEnabled;
    public bool IsReadyToFire => Time.time >= nextFireTimeSeconds && canAttack;

    public void Initialize(WeaponDefinitionSO definition, GameObject newOwner)
    {
        weaponDefinition = definition;
        owner = newOwner;

        fireRatePerSecond = definition.BaseFireRatePerSecond;
        projectileAmount = definition.BaseProjectileAmount;
        piercing = definition.BasePiercing;
        autoFireEnabled = definition.SupportsAutoFire;

        OnInitialized(definition, newOwner);
    }

    protected virtual void OnInitialized(WeaponDefinitionSO definition, GameObject newOwner) { }

    public void ApplyUpgrade(WeaponUpgradeSO upgrade)
    {
        if (upgrade == null) return;

        bool applies =
            upgrade.Scope == WeaponUpgradeScope.AllWeapons ||
            (upgrade.Scope == WeaponUpgradeScope.SpecificWeapon && upgrade.TargetWeaponType == WeaponType);

        if (!applies) return;

        switch (upgrade.UpgradeType)
        {
            case WeaponUpgradeType.FireRate:
                // Example: +0.25f => +25% fire rate
                fireRatePerSecond = Mathf.Max(0.01f, fireRatePerSecond * (1f + upgrade.FireRatePercentValue));
                break;

            case WeaponUpgradeType.ProjectileAmount:
                projectileAmount = Mathf.Max(1, projectileAmount + upgrade.FlatValue);
                break;

            case WeaponUpgradeType.Piercing:
                piercing = Mathf.Max(0, piercing + upgrade.FlatValue);
                break;
        }

        OnStatsChanged();
    }

    protected virtual void OnStatsChanged() { }

    public void FireOnce()
    {
        if (!canAttack) return;
        if (!IsReadyToFire) return;

        float minInterval = fireRatePerSecond > 0f ? 1f / fireRatePerSecond : 0.5f;

        float burstDuration = ExecuteFirePattern(); // subclass returns actual total time used by the pattern
        nextFireTimeSeconds = Time.time + Mathf.Max(minInterval, burstDuration);
    }

    public void StartAutoFire()
    {
        if (!autoFireEnabled) return;
        if (autoFireCoroutine != null) return;
        autoFireCoroutine = StartCoroutine(AutoFireRoutine());
    }

    public void StopAutoFire()
    {
        if (autoFireCoroutine != null)
        {
            StopCoroutine(autoFireCoroutine);
            autoFireCoroutine = null;
        }
    }

    public void SetCanAttack(bool value)
    {
        canAttack = value;
        if (!canAttack) StopAutoFire();
    }

    public GameObject GetOwner() => owner;
    public WeaponDefinitionSO GetDefinition() => weaponDefinition;

    public float GetFireRatePerSecond() => fireRatePerSecond;
    public int GetProjectileAmount() => projectileAmount;
    public int GetPiercing() => piercing;

    /// <summary>Execute the weapon’s emission schedule and return total time consumed this trigger (e.g., burst length).</summary>
    protected abstract float ExecuteFirePattern();

    private IEnumerator AutoFireRoutine()
    {
        while (autoFireEnabled)
        {
            if (IsReadyToFire) FireOnce();
            yield return null;
        }
    }
}
