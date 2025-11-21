using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KunaiWeapon : BaseWeapon
{
    [Header("Fan Damage")]
    [SerializeField] private int damagePerKunai = 1;         // simple per-shot damage

    private Transform fireOrigin;
    private float fanStepDegrees = 5f;

    public void Init(Transform fireOrigin, float fanStepDegrees)
    {
        this.fireOrigin = fireOrigin;
        this.fanStepDegrees = fanStepDegrees;
    }

    protected override float ExecuteFirePattern()
    {
        var def = GetDefinition();
        if (def == null || def.ProjectilePrefab == null || fireOrigin == null)
            return 0f;

        var stats = new WeaponRuntimeStats(
            amount: GetProjectileAmount(),
            limit: def.HorizontalSimultaneousLimit,          // not used
            overflow: def.OverflowResolution,                // not used
            seq: def.SequentialShotIntervalSeconds,          // used: per-shot delay
            alt: def.AlternatingBurstIntervalSeconds,        // not used
            fan: def.MaxFanAngleTotalDegrees                 // used: cone cap
        );

        var ctx = new PatternContext(
            origin: fireOrigin,
            spacing: 0f,
            rowYOffset: 0f,
            left: null,
            right: null,
            altSideOffsetX: 0.6f,
            fanStepDeg: fanStepDegrees
        );

        IShootPattern pattern = WeaponPatternFactory.Get(WeaponPatternType.FanSequential);
        List<ShotCommand> schedule = pattern.Generate(stats, ctx);

        float burstDuration = 0f;
        if (schedule.Count > 0)
            burstDuration = Mathf.Max(0f, schedule[schedule.Count - 1].timeOffsetSeconds);

        StartCoroutine(FireScheduleCoroutine(schedule));
        return burstDuration;
    }

    private IEnumerator FireScheduleCoroutine(List<ShotCommand> schedule)
    {
        float baseTime = Time.time;
        var def = GetDefinition();

        for (int i = 0; i < schedule.Count; i++)
        {
            ShotCommand cmd = schedule[i];

            yield return PauseAwareCoroutine.Until(baseTime, cmd.timeOffsetSeconds);

            // Play sound for EACH kunai in the sequence
            if (def != null && def.FireSound != null)
            {
                SoundUtils.Play2D(def.FireSound);
            }

            // Spawn from the fire origin with the scheduled rotation
            Vector3 worldPos = fireOrigin.position;
            Quaternion rot = Quaternion.Euler(0f, 0f, cmd.angleDegrees);

            ProjectileBase projectileBase = SpawnProjectile(GetDefinition(), worldPos, rot);
            if (projectileBase == null)
                continue;

            HitCountPolicy policy = GetDefinition() != null
                ? GetDefinition().HitCountPolicy
                : HitCountPolicy.OncePerTargetPerProjectile;

            IProjectile projectile = (IProjectile)projectileBase;
            projectile.Initialize(
                owner: GetOwner() != null ? GetOwner() : gameObject,
                damageAmount: damagePerKunai,
                piercing: GetPiercing(),
                policy: policy
            );

            projectileBase.SetSourceWeapon(WeaponType.Kunai);
        }
    }
}
