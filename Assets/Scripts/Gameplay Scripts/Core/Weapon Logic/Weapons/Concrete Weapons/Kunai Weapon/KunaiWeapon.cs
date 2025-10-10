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

            Vector3 worldPos = fireOrigin.position;
            // Rotate around Z so that local +Y points along the desired angle
            Quaternion rot = Quaternion.Euler(0f, 0f, cmd.angleDegrees);
            GameObject go = Instantiate(projectilePrefab, worldPos, rot);

            int pierce = GetPiercing();
            int damage = damagePerKunai;

            if (go.TryGetComponent<IProjectile>(out var proj))
                proj.Initialize(GetOwner() != null ? GetOwner() : gameObject, damage, pierce);
        }
    }
}



