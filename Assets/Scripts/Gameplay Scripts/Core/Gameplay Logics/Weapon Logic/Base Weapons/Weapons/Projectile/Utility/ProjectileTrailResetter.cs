using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class ProjectileTrailResetter : MonoBehaviour
{
    private TrailRenderer trailRenderer;

    private void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
    }

    private void OnEnable()
    {
        trailRenderer.Clear();
        trailRenderer.emitting = true;
    }

    private void OnDisable()
    {
        trailRenderer.emitting = false;
    }
}
