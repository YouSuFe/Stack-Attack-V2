using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BossAddsProfile", menuName = "Level/Boss/ Adds Profile", order = 0)]
public class BossAddsProfile : ScriptableObject
{
    [Header("Wave Timing")]
    [Tooltip("Seconds between waves. Example: 0.5")]
    public float waveInterval = 0.5f;

    [Tooltip("Extra random seconds added to interval each wave.")]
    public float waveIntervalJitter = 0f;

    [Tooltip("Min/Max enemies per wave (inclusive).")]
    public Vector2Int waveCountRange = new Vector2Int(1, 4);

    [Header("Lanes (centered)")]
    [Tooltip("How many horizontal lanes to choose from (centered).")]
    public int laneCount = 5;

    [Tooltip("World units between adjacent lanes (match your grid column width).")]
    public float laneSpacing = 1.0f;

    [Tooltip("Spawn Y = camera top + this offset.")]
    public float topYOffset = 1f;

    [Serializable]
    public class WeightedPrefab
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float weight = 1f;
    }

    [Serializable]
    public class WeightedMovement
    {
        public MovementDefinition movement;
        [Range(0f, 1f)] public float weight = 1f;
    }

    [Tooltip("Candidate enemy prefabs with weights.")]
    public List<WeightedPrefab> prefabs = new List<WeightedPrefab>();

    [Tooltip("Candidate movement definitions with weights (e.g., StraightDown, ZigZag, etc.).")]
    public List<WeightedMovement> movements = new List<WeightedMovement>();
}
