using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Level/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [SerializeField] private List<LevelSegment> segments = new();

    public List<LevelSegment> Segments => segments;
}
