using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Movement/ZigZag Horizontal")]
public class ZigZagHorizontalDefinition : MovementDefinition
{
    #region Inspector
    [Tooltip("Horizontal amplitude (world units).")]
    [SerializeField, Range(0f, 10f)] private float amplitude = 1f;

    [Tooltip("Oscillation frequency (cycles/sec).")]
    [SerializeField, Range(0f, 10f)] private float frequency = 1.25f;

    [Tooltip("Downward speed (units/sec). 0 keeps this motion purely horizontal.")]
    [SerializeField, Range(0f, 50f)] private float verticalSpeed = 0f;
    #endregion

    public override void AttachTo(GameObject target, GridConfig grid, Vector2Int gridCell, List<string> tags)
    {
        var mover = target.GetComponent<ZigZagHorizontalMover>();
        if (!mover) mover = target.AddComponent<ZigZagHorizontalMover>();
        mover.SetParameters(amplitude, frequency, Mathf.Max(0f, verticalSpeed));
    }
}
