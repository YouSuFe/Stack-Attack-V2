using System.Collections.Generic;
using UnityEngine;

public class LaserSkill : ISpecialSkill
{
    private readonly Transform origin;
    private readonly SpecialSkillDefinitionSO def;

    private GameObject owner;
    private GameObject visualInstance;
    private LaserBeamVisual beamVisual;
    private LineRenderer lineRenderer; // cached fallback if no LaserBeamVisual

    // World-space snapshot taken at activation so the beam doesn't follow the origin.
    private Vector2 startSnapshot;
    private Vector2 dirSnapshot;
    private float rangeSnapshot;

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

        if (def.BeamVisualPrefab != null)
        {
            Vector3 spawnPos = origin ? origin.position : Vector3.zero;
            Quaternion spawnRot = origin ? origin.rotation : Quaternion.identity;

            // IMPORTANT: instantiate WITHOUT a parent so it won't follow the origin transform.
            visualInstance = Object.Instantiate(def.BeamVisualPrefab, spawnPos, spawnRot);
            visualInstance.SetActive(false);

            // Prefer helper component
            beamVisual = visualInstance.GetComponent<LaserBeamVisual>();

            // Cache LineRenderer as fallback (in case helper not added)
            if (beamVisual == null)
            {
                lineRenderer = visualInstance.GetComponent<LineRenderer>();
                if (lineRenderer != null) lineRenderer.useWorldSpace = true;
            }
            else
            {
                lineRenderer = beamVisual.Line; // cache for UpdateVisual()
                if (lineRenderer != null) lineRenderer.useWorldSpace = true;
            }
        }
        else
        {
            Debug.LogWarning("[LaserSkill] BeamVisualPrefab is not assigned on SpecialSkillDefinitionSO. Visual will be skipped.");
        }
    }

    public bool TryActivate()
    {
        if (origin == null) return false;

        // Reset per-activation state
        countedThisActivation.Clear();
        tickTimer = 0f;
        singleImpactApplied = false;

        // Snapshot the start & direction ONCE so the beam won't follow the player after firing.
        startSnapshot = origin.position;
        dirSnapshot = origin.up.normalized;
        rangeSnapshot = def.MaxRange;

        // Position/rotate the visual to match the snapshot direction (prefab remains unparented)
        if (visualInstance != null)
        {
            visualInstance.transform.position = startSnapshot;
            visualInstance.transform.rotation = Quaternion.FromToRotation(Vector3.up, dirSnapshot);
            visualInstance.SetActive(true);
        }

        // First frame visual update
        UpdateVisual();

        return true;
    }

    public void TickActive(float deltaTime)
    {
        // Visual is driven from snapshots (won't follow moving origin)
        UpdateVisual();

        // Physics from the SNAPSHOT, not the moving origin
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            startSnapshot, def.BeamRadius, dirSnapshot, rangeSnapshot, def.DamageMask);

        if (hits == null || hits.Length == 0) return;

        if (def.DamageMode == SpecialDamageMode.SingleImpact)
        {
            if (singleImpactApplied) return; // already processed this activation
            ApplyHitBatch(hits, /*applyDamage*/ true, /*raiseHitEvents*/ true);
            singleImpactApplied = true;
            return;
        }

        // Continuous damage mode: apply on a tick interval
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

    private void UpdateVisual()
    {
        if (visualInstance == null) return;

        Vector3 start = startSnapshot;
        Vector3 end = startSnapshot + dirSnapshot * rangeSnapshot;

        if (beamVisual != null)
        {
            beamVisual.SetEndpoints(start, end);
            return;
        }

        if (lineRenderer != null)
        {
            if (!lineRenderer.useWorldSpace) lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
    }

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
