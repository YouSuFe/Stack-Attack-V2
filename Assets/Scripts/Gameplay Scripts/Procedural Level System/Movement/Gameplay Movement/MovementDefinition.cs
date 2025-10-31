
using UnityEngine;
using System.Collections.Generic;

public abstract class MovementDefinition : ScriptableObject
{
    // gridCell = spawn cell (world col,row), tags = SpawnEntry.tags (optional)
    public abstract void AttachTo(GameObject go, GridConfig grid, Vector2Int gridCell, List<string> tags);
}
