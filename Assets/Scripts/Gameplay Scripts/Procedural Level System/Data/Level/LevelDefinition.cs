using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Level/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [SerializeField] private List<LevelSegment> segments = new();

    public List<LevelSegment> Segments => segments;
}
