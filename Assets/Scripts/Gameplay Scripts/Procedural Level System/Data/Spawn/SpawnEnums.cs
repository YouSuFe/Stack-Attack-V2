public enum SegmentType
{
    EnemyWave,
    Reward,
    Boss,
    Space // 15-row gaps: use this and leave empty
}


public enum SpawnType
{
    Enemy = 0,
    Coin = 1,
    Trap = 2,
    Boss = 3,
    Decoration = 4,
    SegmentEndMarker = 5,
    Multiplier = 6,
    Rage = 7,
    SkillFiller = 8,
    Bomb = 9,
    Wall = 10,
}
