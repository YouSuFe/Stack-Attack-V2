using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicWeapon : BaseWeapon
{

    [Header("Basic Damage")]
    [SerializeField] private int damagePerBasic = 1;         // simple per-shot damage

    private Transform fireOrigin;
    private float horizontalSpacing = 0.7f;
    private float secondRowVerticalOffset = 0.15f;

    /// <summary>Called by WeaponDriver when equipping. Injects scene mounts.</summary>
    public void Init(Transform fireOrigin, float horizontalSpacing, float secondRowVerticalOffset)
    {
        this.fireOrigin = fireOrigin;
        this.horizontalSpacing = horizontalSpacing;
        this.secondRowVerticalOffset = secondRowVerticalOffset;
    }
    protected override float ExecuteFirePattern()
    {
        var def = GetDefinition();
        if (def == null || def.ProjectilePrefab == null || fireOrigin == null)
            return 0f;

        // Build runtime stats from current values & definition knobs
        var stats = new WeaponRuntimeStats(
            amount: GetProjectileAmount(),
            limit: def.HorizontalSimultaneousLimit,
            overflow: def.OverflowResolution,
            seq: def.SequentialShotIntervalSeconds,
            alt: def.AlternatingBurstIntervalSeconds,
            fan: def.MaxFanAngleTotalDegrees
        );

        var ctx = new PatternContext(
            origin: fireOrigin,
            spacing: horizontalSpacing,
            rowYOffset: secondRowVerticalOffset
        );

        // ask the right pattern for a schedule
        IShootPattern pattern = WeaponPatternFactory.Get(def.PatternType);
        List<ShotCommand> schedule = pattern.Generate(stats, ctx);

        // burstDuration = time of the last scheduled shot (0 if only instant shots)
        float burstDuration = 0f;
        if (schedule.Count > 0)
            burstDuration = Mathf.Max(0f, schedule[schedule.Count - 1].timeOffsetSeconds);

        // actually spawn on a coroutine
        StartCoroutine(FireScheduleCoroutine(def.ProjectilePrefab, schedule));

        return burstDuration;
    }

    private IEnumerator FireScheduleCoroutine(GameObject projectilePrefab, List<ShotCommand> schedule)
    {
        float baseTime = Time.time;

        for (int i = 0; i < schedule.Count; i++)
        {
            ShotCommand cmd = schedule[i];

            float targetTime = baseTime + cmd.timeOffsetSeconds;

            // Wait until it's time to spawn this command
            while (Time.time < targetTime)
                yield return null;

            // Spawn
            Vector3 worldPos = fireOrigin.TransformPoint(cmd.localOffset);
            Quaternion rot = Quaternion.Euler(0f, 0f, cmd.angleDegrees);
            GameObject go = Instantiate(projectilePrefab, worldPos, rot);

            HitCountPolicy policy = GetDefinition() != null
                ? GetDefinition().HitCountPolicy
                : HitCountPolicy.OncePerTargetPerProjectile;

            int damage = damagePerBasic; // fixed for now, can be data-driven later
            int pierce = GetPiercing();

            if (go.TryGetComponent<IProjectile>(out var projectile))
            {
                projectile.Initialize(
                    GetOwner() != null ? GetOwner() : gameObject,
                    damage,
                    pierce,
                    policy
                );
            }
        }

    }
}
