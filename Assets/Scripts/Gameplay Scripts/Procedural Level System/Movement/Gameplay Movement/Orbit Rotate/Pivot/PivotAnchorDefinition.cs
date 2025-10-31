using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Movement/Pivot Anchor")]
public class PivotAnchorDefinition : MovementDefinition
{
    #region Inspector
    [Header("Registry / Behavior")]
    [Tooltip("Registry key shared by followers (OrbitAroundAnchor).")]
    [SerializeField] private string defaultAnchorKey = "RingA";

    [Tooltip("Angular speed (deg/sec) for the formation.")]
    [SerializeField, Range(0f, 720f)] private float angularSpeedDegPerSec = 90f;

    [Tooltip("Downward speed (units/sec) applied AFTER activation (post gate handoff).")]
    [SerializeField, Range(0f, 50f)] private float verticalSpeed = 0f;

    [Header("Prefab (Required for target==null)")]
    [Tooltip("PivotAnchor prefab (should include SpawnStageAgent, tagged 'Pivot', on Staging layer).")]
    [SerializeField] private PivotAnchor anchorPrefab = null;

    [Header("Optional: If a dummy SpawnEntry GameObject is provided")]
    [Tooltip("If true and a dummy target exists, convert that dummy into the actual PivotAnchor.")]
    [SerializeField] private bool useDummyAsAnchor = true;

    [Tooltip("If not using dummy, destroy the dummy marker after prefab spawn.")]
    [SerializeField] private bool destroyDummyAfterPrefab = true;

    [Header("Gate / Staging Integration")]
    [Tooltip("Tag used by EntryGate filtering (e.g., 'Pivot').")]
    [SerializeField] private string pivotTag = "Pivot";

    [Tooltip("Staging layer name used off-screen; EntryGate filters this layer.")]
    [SerializeField] private string stagingLayerName = "Staging";
    #endregion

    public override void AttachTo(GameObject target, GridConfig grid, Vector2Int cell, List<string> tags)
    {
        // --- 1) Resolve KEY and desired PIVOT POSITION from tags ---
        string key = defaultAnchorKey;

        // By default (when target is null), we use the Grid world position (original behavior).
        Vector3 pivotWorld = GridMath.GridToWorld(cell.x, cell.y, grid);

        if (tags != null)
        {
            foreach (var sRaw in tags)
            {
                var s = sRaw?.Trim();
                if (string.IsNullOrEmpty(s)) continue;

                if (s.StartsWith("anchor=", StringComparison.OrdinalIgnoreCase))
                {
                    key = s.Substring(7).Trim();
                }
                else if (s.Equals("pivot=here", StringComparison.OrdinalIgnoreCase))
                {
                    pivotWorld = GridMath.GridToWorld(cell.x, cell.y, grid);
                }
                else if (s.StartsWith("pivot=", StringComparison.OrdinalIgnoreCase))
                {
                    // pivot=c,r
                    var payload = s.Substring(6).Trim();
                    var parts = payload.Split(',');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out int c) &&
                        int.TryParse(parts[1], out int r))
                    {
                        pivotWorld = GridMath.GridToWorld(c, r, grid);
                    }
                }
            }
        }

        // If a dummy target exists, we can prefer its CURRENT world position
        // (authoring accurate, avoids grid float rounding or parent offsets)
        if (target != null && useDummyAsAnchor)
        {
            pivotWorld = target.transform.position;
        }

        // --- 2) Create / get the PivotAnchor at EXACT world position ---
        PivotAnchor anchorInstance = null;

        if (target == null)
        {
            // ORIGINAL BEHAVIOR PATH: there is no dummy. We must instantiate a prefab.
            if (anchorPrefab == null)
            {
                Debug.LogError("[PivotAnchorDefinition] anchorPrefab is not assigned but target is null. " +
                               "Assign a prefab to use when no dummy is provided.");
                return;
            }

            // Instantiate UNPARENTED at the exact world position to avoid segment-root double offsets.
            anchorInstance = Instantiate(anchorPrefab, pivotWorld, Quaternion.identity, null);
            EnsureStagingIntegration(anchorInstance.gameObject);
        }
        else
        {
            if (useDummyAsAnchor)
            {
                // IMPORTANT: Order is important between PivotAnchor and SpawnStateAgent due to subscribe logic
                EnsureStagingIntegration(target);
                // Convert the dummy into the anchor (no parenting surprises).
                if (!target.TryGetComponent(out anchorInstance))
                    anchorInstance = target.AddComponent<PivotAnchor>();

                anchorInstance.transform.position = pivotWorld; // enforce exact world pos
            }
            else
            {
                // Instantiate a separate prefab AT THE DUMMY'S WORLD POSITION and keep it UNPARENTED
                if (anchorPrefab == null)
                {
                    Debug.LogError("[PivotAnchorDefinition] anchorPrefab is not assigned and useDummyAsAnchor=false.");
                    return;
                }

                // Use the dummy's *current* world position (already includes segment alignment)
                Vector3 worldFromDummy = target.transform.position;
                anchorInstance = UnityEngine.Object.Instantiate(anchorPrefab, worldFromDummy, Quaternion.identity, null);
                EnsureStagingIntegration(anchorInstance.gameObject);

                if (destroyDummyAfterPrefab)
                {
                    Destroy(target);
                }

                // Note: we deliberately DO NOT parent under the segment root to avoid double offsets.
            }
        }

        if (anchorInstance == null)
        {
            Debug.LogError("[PivotAnchorDefinition] Failed to create or obtain PivotAnchor instance.");
            return;
        }

        // Make sure final world position is enforced (paranoia)
        anchorInstance.transform.position = pivotWorld;

        // --- 3) Configure behavior (AngleDeg always ticks; vertical drift only after activation) ---
        anchorInstance.Configure(key, angularSpeedDegPerSec, Mathf.Max(0f, verticalSpeed));
    }

    #region Helpers
    private void EnsureStagingIntegration(GameObject go)
    {
        // Tag for gate filtering
        if (!string.IsNullOrEmpty(pivotTag))
            go.tag = pivotTag;

        // Staging layer so off-screen physics are ignored; agent switches to gameplay layer at drag start.
        int stagingLayer = LayerMask.NameToLayer(stagingLayerName);
        if (stagingLayer != -1)
            go.layer = stagingLayer;

        // Agent (conveyor + handoff)
        if (!go.TryGetComponent<SpawnStageAgent>(out _))
            go.AddComponent<SpawnStageAgent>();

        // Collider so EntryGate trigger sees it (if Gate lacks a Rigidbody2D)
        if (!go.TryGetComponent<Collider2D>(out var col2d))
        {
            col2d = go.AddComponent<CircleCollider2D>();
            col2d.isTrigger = true;
        }
    }
    #endregion
}
