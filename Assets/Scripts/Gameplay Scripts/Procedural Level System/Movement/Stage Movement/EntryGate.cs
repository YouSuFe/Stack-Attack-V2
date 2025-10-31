using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Trigger collider placed just above the camera view. 
/// When staged objects cross it, we drag them to targetY and then resume their mover.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EntryGate : MonoBehaviour
{
    #region Serialized
    [Header("Gate Settings")]
    [SerializeField, Tooltip("World Y the object is dragged to during handoff.")]
    private Transform targetPoint;

    [SerializeField, Tooltip("Optional world-space anchor for bosses. If assigned and the object has Boss tag, it will be dragged here.")]
    private Transform bossSpawnPoint;

    [SerializeField, Tooltip("Tag checked on SpawnStageAgent to decide boss routing.")]
    private string bossTag = "Boss";

    [SerializeField, Tooltip("Drag duration in seconds for the handoff.")]
    private float dragDuration = 0.5f;

    [Header("Filtering")]
    [SerializeField, Tooltip("Only process objects on these layers (recommended: Staging).")]
    private LayerMask allowedLayers;

    [SerializeField, Tooltip("Optional: Only process these tags. Leave empty to accept all.")]
    private List<string> allowedTags = new List<string>();
    #endregion

    #region Unity Lifecycle
    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!LayerAllowed(other.gameObject.layer)) return;
        if (!TagAllowed(other.tag)) return;

        if (!other.TryGetComponent<SpawnStageAgent>(out var agent))
            return;

        if (other.TryGetComponent<OrbitAroundAnchorMover>(out var orbit))
        {
            var a = PivotAnchor.Find(GetAnchorKeySafe(orbit)); // helper below or just orbit-anchor lookup
            if (a != null && !a.GroupHandoffStarted)
                return; // ignore this entry; follower stays on conveyor until pivot begins
        }


        // Safety check: if no target assigned, do nothing
        if (targetPoint == null)
        {
            Debug.LogWarning($"[EntryGate] No target point assigned. Object {other.name} skipped.");
            return;
        }

        if (agent.CompareTag(bossTag) && bossSpawnPoint != null)
        {
            agent.BeginHandoffTo(bossSpawnPoint.position, dragDuration);
            return;
        }
        // Begin the handoff using the target point's Y
        agent.BeginHandoff(targetPoint.position.y, dragDuration);
    }
    #endregion

    #region Helpers
    private bool LayerAllowed(int objectLayer)
    {
        return (allowedLayers.value & (1 << objectLayer)) != 0;
    }

    private bool TagAllowed(string tag)
    {
        if (allowedTags == null || allowedTags.Count == 0) return true;
        return allowedTags.Contains(tag);
    }

    private static string GetAnchorKeySafe(OrbitAroundAnchorMover mover)
    {
        var t = mover.GetType();
        var f = t.GetField("anchorKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null ? (string)f.GetValue(mover) : null;
    }
    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (targetPoint == null)
            return;

        // Draw gate collider outline
        Gizmos.color = Color.cyan;
        var c = GetComponent<Collider2D>();
        if (c is BoxCollider2D b)
            Gizmos.DrawWireCube(transform.position + (Vector3)b.offset, b.size);
        else if (c is CircleCollider2D circle)
            Gizmos.DrawWireSphere(transform.position + (Vector3)circle.offset, circle.radius);

        // Draw target Y line
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(-1000, targetPoint.position.y, 0),
                        new Vector3(1000, targetPoint.position.y, 0));
        Gizmos.DrawSphere(targetPoint.position, 0.1f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(-1000, bossSpawnPoint.position.y, 0),
                        new Vector3(1000, bossSpawnPoint.position.y, 0));
        Gizmos.DrawSphere(bossSpawnPoint.position, 0.1f);
    }
#endif
}
