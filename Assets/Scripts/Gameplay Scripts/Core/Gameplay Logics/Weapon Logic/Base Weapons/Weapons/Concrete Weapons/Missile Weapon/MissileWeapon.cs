using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileWeapon : BaseWeapon
{

    [Header("Damage")]
    [SerializeField] private int damagePerMissile = 2;

    [Header("Angle")]
    [SerializeField, Range(0f, 5f)] private float angleJitterDegrees = 1.5f;

    private Transform fireOrigin;
    private Transform leftMuzzleTransform;
    private Transform rightMuzzleTransform;
    private float fallbackSideOffsetX = 0.6f;

    public void Init(Transform fireOrigin, Transform leftMuzzle, Transform rightMuzzle, float fallbackSideOffsetX)
    {
        this.fireOrigin = fireOrigin;
        this.leftMuzzleTransform = leftMuzzle;
        this.rightMuzzleTransform = rightMuzzle;
        this.fallbackSideOffsetX = fallbackSideOffsetX;
    }

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

        StartCoroutine(FireScheduleCoroutine(schedule));
        return burstDuration;
    }

    private IEnumerator FireScheduleCoroutine(List<ShotCommand> schedule)
    {
        float baseTime = Time.time;

        for (int i = 0; i < schedule.Count; i++)
        {
            ShotCommand cmd = schedule[i];

            yield return PauseAwareCoroutine.Until(baseTime, cmd.timeOffsetSeconds);

            // Spawn at the fireOrigin + local offset (which points to the left/right muzzle)
            Vector3 worldPos = fireOrigin.TransformPoint(cmd.localOffset);
            float zJitter = UnityEngine.Random.Range(-angleJitterDegrees, angleJitterDegrees);
            Quaternion rot = fireOrigin.rotation * Quaternion.Euler(0f, 0f, zJitter);

            ProjectileBase projectileBase = SpawnProjectile(GetDefinition(), worldPos, rot);
            if (projectileBase == null)
                continue;

            HitCountPolicy policy = GetDefinition() != null
                ? GetDefinition().HitCountPolicy
                : HitCountPolicy.OncePerTargetPerProjectile;

            IProjectile projectile = (IProjectile)projectileBase;
            projectile.Initialize(
                owner: GetOwner() != null ? GetOwner() : gameObject,
                damageAmount: damagePerMissile,
                piercing: GetPiercing(),
                policy: policy
            );

            projectileBase.SetSourceWeapon(WeaponType.Missile);

            // Mirror sine phase by side: left (x<0) => π, right => 0
            SineMissileProjectile sine = projectileBase as SineMissileProjectile;
            if (sine != null)
            {
                float phase = (cmd.localOffset.x < 0f) ? Mathf.PI : 0f;
                sine.SetPhaseOffsetRadians(phase);
            }
        }
    }

}