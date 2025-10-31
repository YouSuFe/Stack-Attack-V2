using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct EnemyColorSelection
{
    public EnemyHue hue;
    public EnemyTone tone;

    public EnemyColorSelection(EnemyHue defaultHue, EnemyTone defaultTone)
    {
        hue = defaultHue;
        tone = defaultTone;
    }
}

[Serializable]
public class SpawnEntry
{
    #region Core Placement
    [Tooltip("Segment-local row [0..LengthInRows-1]")]
    public int rowOffset;

    [Tooltip("Column index [0..Columns-1] (validated per active stripe GridConfig)")]
    public int column;

    [Tooltip("What to spawn at this cell.")]
    public SpawnType spawnType;

    [Tooltip("Optional payload for this spawn, e.g., MovementDefinition for enemies.")]
    public MovementDefinition payload;

    [Tooltip("Reserved for stacked spawns (>=1).")]
    public int count = 1;

    [Tooltip("Reserved spacing between stacked spawns in world rows (>=1).")]
    public int spacing = 1;

    [Tooltip("Free-form tags like \"anchor=PackA\", \"pivot=2,160\" or \"pivot=here\".")]
    public List<string> tags = new();
    #endregion

    #region Enemy Color (used only when spawnType == Enemy)
    [SerializeField, Tooltip("Only used when SpawnType == Enemy. Chosen hue & tone resolved via EnemyColorPalette.")]
    private EnemyColorSelection enemyColor = new EnemyColorSelection(EnemyHue.Red, EnemyTone.Normal);

    public EnemyColorSelection EnemyColor => enemyColor;

    /// <summary>Assign enemy color (no effect unless spawnType == Enemy).</summary>
    public void SetEnemyColor(EnemyHue hue, EnemyTone tone)
    {
        enemyColor = new EnemyColorSelection(hue, tone);
    }

    /// <summary>Resolve the actual Color via a palette. Returns false if palette missing or hue not found.</summary>
    public bool TryResolveEnemyColor(EnemyColorPalette palette, out Color color)
    {
        if (spawnType == SpawnType.Enemy && palette != null)
        {
            return palette.TryResolve(enemyColor.hue, enemyColor.tone, out color);
        }
        color = Color.white;
        return false;
    }
    #endregion

    #region Validation Helpers (optional)
    public void ClampReserved()
    {
        if (count < 1) count = 1;
        if (spacing < 1) spacing = 1;
    }
    #endregion
}
