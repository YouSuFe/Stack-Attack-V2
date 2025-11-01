using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Level/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    [SerializeField] private List<LevelDefinition> levels = new();
    public int Count => levels?.Count ?? 0;
    public LevelDefinition Get(int index) => (levels != null && index >= 0 && index < levels.Count) ? levels[index] : null;
}
