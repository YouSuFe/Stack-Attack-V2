using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyHue
{
    Red, Blue, Green, Yellow, Purple, Orange, Cyan, Magenta, White, Black
}

public enum EnemyTone
{
    Light, Normal, Deep
}

[CreateAssetMenu(fileName = "EnemyColorPalette", menuName = "Game/Colors/Enemy Color Palette")]
public class EnemyColorPalette : ScriptableObject
{
    [Serializable]
    public struct Swatch
    {
        public EnemyHue hue;
        public Color light;
        public Color normal;
        public Color deep;

        public Color Resolve(EnemyTone tone)
        {
            switch (tone)
            {
                case EnemyTone.Light: return light;
                case EnemyTone.Deep: return deep;
                default: return normal;
            }
        }
    }

    [SerializeField] private List<Swatch> swatches = new List<Swatch>();

    public bool TryResolve(EnemyHue hue, EnemyTone tone, out Color color)
    {
        for (int i = 0; i < swatches.Count; i++)
        {
            if (swatches[i].hue == hue)
            {
                color = swatches[i].Resolve(tone);
                return true;
            }
        }
        color = Color.white;
        return false;
    }
}
