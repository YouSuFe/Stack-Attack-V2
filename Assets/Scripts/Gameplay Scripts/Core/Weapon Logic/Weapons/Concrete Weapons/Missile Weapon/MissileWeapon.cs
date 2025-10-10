using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileWeapon : BaseWeapon
{
    [Header("Firing Origin & Muzzles")]
    [SerializeField] private Transform fireOrigin;
    [SerializeField] private Transform leftMuzzleTransform;   // optional but recommended
    [SerializeField] private Transform rightMuzzleTransform;  // optional but recommended
    [SerializeField] private float fallbackSideOffsetX = 0.6f;

    [Header("Damage")]
    [SerializeField] private int damagePerMissile = 2;

    protected override float ExecuteFirePattern()
    {
        var def = GetDefinition();
        if (def == null || def.ProjectilePrefab == null || fireOrigin == null)
            return 0f;

        var stats = new WeaponRuntimeStats(
            amount: GetProjectileAmount(),
            limit: def.HorizontalSimultaneousLimit,
            overflow: def.OverflowResolution,
            seq: def.SequentialShotIntervalSeconds,
            alt: def.AlternatingBurstIntervalSeconds,  // used here
            fan: def.MaxFanAngleTotalDegrees
        );

        var ctx = new PatternContext(
            origin: fireOrigin,
            spacing: 0f,
            rowYOffset: 0f,
            left: leftMuzzleTransform,
            right: rightMuzzleTransform,
            altSideOffsetX: fallbackSideOffsetX
        );

        IShootPattern pattern = WeaponPatternFactory.Get(WeaponPatternType.AlternatingBurst);
        List<ShotCommand> schedule = pattern.Generate(stats, ctx);

        float burstDuration = 0f;
        if (schedule.Count > 0)
            burstDuration = Mathf.Max(0f, schedule[schedule.Count - 1].timeOffsetSeconds);

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

            while (Time.time < targetTime)
                yield return null;

            // Spawn at the fireOrigin + local offset (which points to the left/right muzzle)
            Vector3 worldPos = fireOrigin.TransformPoint(cmd.localOffset);
            Quaternion rot = fireOrigin.rotation;
            GameObject go = Instantiate(projectilePrefab, worldPos, rot);

            // Initialize projectile (owner, damage, pierce)
            int pierce = GetPiercing();
            int damage = damagePerMissile;

            if (go.TryGetComponent<IProjectile>(out var projectile))
                projectile.Initialize(GetOwner() != null ? GetOwner() : gameObject, damage, pierce);

            // Mirror sine phase by side: left (x<0) => π, right => 0
            if (go.TryGetComponent<SineMissileProjectile>(out var sine))
            {
                float phase = (cmd.localOffset.x < 0f) ? Mathf.PI : 0f;
                sine.SetPhaseOffsetRadians(phase);
            }
        }
    }
}


