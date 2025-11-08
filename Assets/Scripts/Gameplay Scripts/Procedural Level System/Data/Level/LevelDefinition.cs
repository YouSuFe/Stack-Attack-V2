using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Level/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    #region Private Fields
    [Header("Segments")]
    [SerializeField] private List<LevelSegment> segments = new();

    [Header("Rewards")]
    [SerializeField, Tooltip("Per-level reward tuning (coin value, XP caps/floors).")]
    private LevelRewardDefinition rewardDefinition;
    #endregion

    #region Public API
    public List<LevelSegment> Segments => segments;
    public LevelRewardDefinition RewardDefinition => rewardDefinition;
    #endregion
}
