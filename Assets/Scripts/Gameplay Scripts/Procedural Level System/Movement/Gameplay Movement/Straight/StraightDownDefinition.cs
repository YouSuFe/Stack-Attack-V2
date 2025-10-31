using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Movement/Straight Down")]
public class StraightDownDefinition : MovementDefinition
{
    #region Inspector
    [Tooltip("Downward speed (units/sec). 0 means no movement.")]
    [SerializeField, Range(0f, 50f)] private float verticalSpeed = 3f;
    #endregion

    public override void AttachTo(GameObject target, GridConfig grid, Vector2Int gridCell, List<string> tags)
    {
        var mover = target.GetComponent<StraightDownMover>();
        if (!mover) mover = target.AddComponent<StraightDownMover>();
        mover.SetParameters(Mathf.Max(0f, verticalSpeed));
    }
}
