using System.Collections.Generic;
using UnityEngine;

public class LaserSkill : ISpecialSkill
{
    private readonly Transform origin;
    private readonly SpecialSkillDefinitionSO def;

    private GameObject owner;
    private GameObject visualInstance;
    private LineRenderer lineRenderer;

    // Hit accounting for this activation
    private readonly HashSet<int> countedThisActivation = new();

    // Timing for Continuous mode
    private float tickTimer;
    private bool singleImpactApplied;

    public LaserSkill(Transform origin, SpecialSkillDefinitionSO def)
    {
        this.origin = origin;
        this.def = def;
    }

    public void Initialize(SpecialSkillDefinitionSO definition, GameObject owner)
    {
        this.owner = owner;

        if (def.BeamVisualPrefab != null && origin != null)
        {
            visualInstance = Object.Instantiate(def.BeamVisualPrefab, origin.position, origin.rotation, origin);
            lineRenderer = visualInstance.GetComponent<LineRenderer>();
            if (visualInstance) visualInstance.SetActive(false);
        }
    }

    public bool TryActivate()
    {
        if (origin == null) return false;

        // Reset per-activation state
        countedThisActivation.Clear();
        tickTimer = 0f;
        singleImpactApplied = false;

        if (visualInstance != null) visualInstance.SetActive(true);
        return true;
    }

    public void TickActive(float deltaTime)
    {
        if (origin == null) return;

        Vector2 start = origin.position;
        Vector2 dir = origin.up;
        float range = def.MaxRange;

        // Visuals
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, start + dir * range);
        }

        // Collect current hits
        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, def.BeamRadius, dir, range, def.DamageMask);
        if (hits == null || hits.Length == 0) return;

        if (def.DamageMode == SpecialDamageMode.SingleImpact)
        {
            if (singleImpactApplied) return; // already did our one-time hit this activation
            ApplyHitBatch(hits, /*applyDamage*/ true, /*raiseHitEvents*/ true);
            singleImpactApplied = true;
            return;
        }

        // Continuous: accumulate time and apply on interval
        tickTimer += deltaTime;
        if (tickTimer >= def.TickIntervalSeconds)
        {
            tickTimer -= def.TickIntervalSeconds;
            ApplyHitBatch(hits, /*applyDamage*/ true, /*raiseHitEvents*/ true);
        }
    }

    public void Stop()
    {
        if (visualInstance != null)
            visualInstance.SetActive(false);
    }

    // -------- Helpers --------

    private void ApplyHitBatch(RaycastHit2D[] hits, bool applyDamage, bool raiseHitEvents)
    {
        int damagePerHit = Mathf.Max(1, def.DamagePerTick);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null) continue;

            if (!col.TryGetComponent<IDamageable>(out var target))
                continue;

            // Damage
            if (applyDamage)
                target.TakeDamage(damagePerHit, owner);

            // Charge: raise player-hit event (obeys "count once per activation" if configured)
            if (raiseHitEvents && owner != null)
            {
                int id = (target as Component) ? ((Component)target).transform.root.GetInstanceID()
                                               : col.GetInstanceID();

                if (!def.CountOncePerActivation || countedThisActivation.Add(id))
                {
                    HitEventBus.RaisePlayerHit(target, owner);
                }
            }
        }
    }
}
