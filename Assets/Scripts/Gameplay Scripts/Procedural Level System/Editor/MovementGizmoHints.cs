using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public static class MovementGizmoHints
{
    public static void DrawArrow(Vector3 from, Vector3 to, float head = 0.15f)
    {
        Handles.DrawLine(from, to);
        var dir = (to - from).normalized;
        var right = new Vector3(-dir.y, dir.x, 0f);
        Handles.DrawLine(to, to - dir * head + right * head * 0.5f);
        Handles.DrawLine(to, to - dir * head - right * head * 0.5f);
    }

    public static void DrawZig(Vector3 center, float width, float height)
    {
        int steps = 10;
        Vector3 prev = center + new Vector3(-width * 0.5f, 0f, 0f);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float x = Mathf.Lerp(-0.5f, 0.5f, t) * width;
            float y = Mathf.Sin(t * Mathf.PI * 2f) * height * 0.2f;
            var p = center + new Vector3(x, y, 0f);
            Handles.DrawLine(prev, p);
            prev = p;
        }
    }
}
#endif
