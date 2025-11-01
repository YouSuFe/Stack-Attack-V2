 using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Movement/Orbit Around Anchor")]
public class OrbitAroundAnchorDefinition : MovementDefinition
{
    #region Inspector
    [Tooltip("Anchor key to bind to if no tag overrides it.")]
    [SerializeField] private string defaultAnchorKey = "RingA";

    [Tooltip("If > 0, forces a radius; otherwise radius is inferred from spawn position.")]
    [SerializeField] private float radiusOverride = -1f;

    [Tooltip("Per-follower phase offset (degrees).")]
    [SerializeField] private float phaseOffsetDeg = 0f;

    [Tooltip("Personal downward speed (units/sec). 0 keeps rigid formation.")]
    [SerializeField, Range(0f, 50f)] private float verticalSpeed = 0f;
    #endregion

    public override void AttachTo(GameObject go, GridConfig grid, Vector2Int gridCell, List<string> tags)
    {
        if (!go.TryGetComponent(out OrbitAroundAnchorMover mover))
            mover = go.AddComponent<OrbitAroundAnchorMover>();

        string anchorKey = defaultAnchorKey;
        float phase = phaseOffsetDeg;
        float r = radiusOverride;
        float personalVelocity = verticalSpeed;

        // Simple inline tag parsing; no helpers required.
        if (tags != null)
        {
            foreach (var t in tags)
            {
                if (string.IsNullOrEmpty(t)) continue;
                var kv = t.Split('=');
                var k = kv[0].Trim().ToLowerInvariant();

                if (k == "anchor" && kv.Length > 1) anchorKey = kv[1].Trim();
                else if (k == "phase" && kv.Length > 1 && float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ph))
                    phase = ph;
                else if (k == "r" && kv.Length > 1 && float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rr))
                    r = rr;
                else if (k == "v" && kv.Length > 1 && float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vv))
                    personalVelocity = vv;
            }
        }

        // If author explicitly gave phase=..., do NOT lock from spawn; otherwise DO lock.
        bool lockPhaseFromSpawn = true;
        if (tags != null)
        {
            foreach (var t in tags)
                if (!string.IsNullOrEmpty(t) && t.Trim().StartsWith("phase=", System.StringComparison.OrdinalIgnoreCase))
                    lockPhaseFromSpawn = false;
        }

        mover.Configure(anchorKey, r, phase, personalVelocity, lockPhaseFromSpawn);
    }

}
