using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicWeapon : BaseWeapon
{
    [Header("Firing Origin & Layout")]
    [SerializeField] private Transform fireOrigin;          // usually a child at player’s muzzle
    [SerializeField] private float horizontalSpacing = 0.7f;
    [SerializeField] private float secondRowVerticalOffset = 0.15f; // for OverflowResolution.Rows

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
        int index = 0;

        while (index < schedule.Count)
        {
            ShotCommand cmd = schedule[index];

            float targetTime = baseTime + cmd.timeOffsetSeconds;
            // wait until it's time to spawn this command
            while (Time.time < targetTime)
                yield return null;

            // spawn
            Vector3 worldPos = fireOrigin.TransformPoint(cmd.localOffset);
            Quaternion rot = Quaternion.Euler(0f, 0f, cmd.angleDegrees);
            GameObject go = Instantiate(projectilePrefab, worldPos, rot);

            int damage = 1; // for now fixed; we can data-drive this later per weapon/upgrade
            int pierce = GetPiercing();

            if (go.TryGetComponent<IProjectile>(out var projectile))
            {
                projectile.Initialize(GetOwner() != null ? GetOwner() : gameObject, damage, pierce);
            }

            index++;
        }
    }
}
