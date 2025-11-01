using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Level/Spawn Prefab Catalog")]
public class SpawnPrefabCatalog : ScriptableObject
{
    [Serializable]
    public struct Binding
    {
        public SpawnType type;
        public GameObject prefab;
    }

    [SerializeField] private Binding[] entries;

    private readonly Dictionary<SpawnType, GameObject> map = new();

    private void OnEnable()
    {
        map.Clear();
        if (entries == null) return;
        for (int i = 0; i < entries.Length; i++)
        {
            var b = entries[i];
            if (!map.ContainsKey(b.type) && b.prefab != null)
                map.Add(b.type, b.prefab);
        }
    }

    public bool TryGetPrefab(SpawnType type, out GameObject prefab)
    {
        return map.TryGetValue(type, out prefab);
    }
}
