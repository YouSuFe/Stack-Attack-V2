using UnityEngine;

[CreateAssetMenu(menuName = "MainMenu/Rank/Rank Sprite Set", fileName = "RankSpriteSet")]
public class RankSpriteSet : ScriptableObject
{
    #region Private Fields
    [SerializeField, Tooltip("Sprite for Bronze major rank.")]
    private Sprite bronze;

    [SerializeField, Tooltip("Sprite for Silver major rank.")]
    private Sprite silver;

    [SerializeField, Tooltip("Sprite for Gold major rank.")]
    private Sprite gold;
    #endregion

    #region Public API
    public Sprite GetSprite(RankMajor major)
    {
        switch (major)
        {
            case RankMajor.Bronze: return bronze;
            case RankMajor.Silver: return silver;
            case RankMajor.Gold: return gold;
            default: return null;
        }
    }
    #endregion
}