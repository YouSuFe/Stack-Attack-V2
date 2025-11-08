using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpecialSkillGizmo2D : MonoBehaviour
{
    #region Serialized Fields
    [Header("Source Data")]
    [SerializeField] private Transform origin;
    [SerializeField] private SpecialSkillDefinitionSO def;

    [Header("Gizmo Options")]
    [SerializeField] private bool drawWhenNotSelected = false;
    [SerializeField] private bool visualizeHits = true;
    [SerializeField, Range(0.05f, 1f)] private float normalLength = 0.35f;
    [SerializeField, Range(0.05f, 1f)] private float arrowHeadSize = 0.25f;
    #endregion

    #region Unity Gizmos
    private void OnDrawGizmos()
    {
        if (drawWhenNotSelected) DrawAll();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawWhenNotSelected) DrawAll();
    }
    #endregion

    #region Drawing
    private void DrawAll()
    {
        if (origin == null || def == null) return;

        Vector2 start = origin.position;
        Vector2 dir = origin.up;                 // matches your TickActive()
        float range = Mathf.Max(0f, def.MaxRange);
        float radius = Mathf.Max(0f, def.BeamRadius);

        // Capsule preview of the CircleCast volume
        DrawCapsule2D(start, dir, range, radius);

#if UNITY_EDITOR
        // Quick labels so you can read values at a glance
        Handles.color = new Color(1f, 1f, 1f, 0.9f);
        Handles.Label(start, $"Start\nr={radius:0.###}");
        Handles.Label(start + dir * range, $"End\nL={range:0.###}");
#endif

        if (!visualizeHits) return;

        // Show the same hits your runtime code would see
        var hits = Physics2D.CircleCastAll(start, radius, dir, range, def.DamageMask);
        if (hits == null || hits.Length == 0) return;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(hit.point, Mathf.Max(0.02f, radius * 0.15f));

            Vector3 nStart = hit.point;
            Vector3 nEnd = nStart + (Vector3)hit.normal * normalLength;
            Gizmos.DrawLine(nStart, nEnd);

#if UNITY_EDITOR
            Handles.color = new Color(1f, 0.5f, 0.5f, 0.95f);
            Handles.Label(nEnd, hit.collider.name);
#endif
        }
    }

    private void DrawCapsule2D(Vector2 start, Vector2 dir, float length, float radius)
    {
        dir = dir.sqrMagnitude > 0f ? dir.normalized : Vector2.up;
        Vector2 right = new Vector2(-dir.y, dir.x);

        Vector3 a = start;
        Vector3 b = start + dir * length;

        Vector3 aRight = a + (Vector3)(right * radius);
        Vector3 aLeft = a - (Vector3)(right * radius);
        Vector3 bRight = b + (Vector3)(right * radius);
        Vector3 bLeft = b - (Vector3)(right * radius);

        // Side rails (silhouette)
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.9f);
        Gizmos.DrawLine(aRight, bRight);
        Gizmos.DrawLine(aLeft, bLeft);

#if UNITY_EDITOR
        // End discs (cleaner with Handles)
        Handles.color = new Color(0f, 0.75f, 1f, 0.9f);
        Handles.DrawWireDisc(a, Vector3.forward, radius);
        Handles.DrawWireDisc(b, Vector3.forward, radius);
#else
        Gizmos.color = new Color(0f, 0.75f, 1f, 0.9f);
        Gizmos.DrawWireSphere(a, radius);
        Gizmos.DrawWireSphere(b, radius);
#endif

        // Direction arrow
        DrawArrow(a, (Vector3)dir * Mathf.Max(0.15f, Mathf.Min(length, 0.75f)));
    }

    private void DrawArrow(Vector3 from, Vector3 vec)
    {
        Vector3 to = from + vec;
        Gizmos.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);
        Gizmos.DrawLine(from, to);

        Vector3 dir = vec.sqrMagnitude > 0f ? vec.normalized : Vector3.up;
        Vector3 right = new Vector3(-dir.y, dir.x, 0f);
        Vector3 headL = to - dir * arrowHeadSize + right * (arrowHeadSize * 0.5f);
        Vector3 headR = to - dir * arrowHeadSize - right * (arrowHeadSize * 0.5f);
        Gizmos.DrawLine(to, headL);
        Gizmos.DrawLine(to, headR);
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug Info")]
    private void DebugInfo()
    {
        if (def == null)
        {
            Debug.Log("No SpecialSkillDefinitionSO assigned.");
            return;
        }
        Debug.Log(
            $"[SpecialSkillGizmo2D]\n" +
            $"- Range: {def.MaxRange}\n" +
            $"- Radius: {def.BeamRadius}\n" +
            $"- DamageMode: {def.DamageMode}\n" +
            $"- Tick Interval: {def.TickIntervalSeconds}\n" +
            $"- LayerMask: {def.DamageMask.value}"
        );
    }
#endif
}
