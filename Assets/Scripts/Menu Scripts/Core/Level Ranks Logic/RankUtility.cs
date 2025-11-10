
/// <summary>
/// Stage number (1-based) -> Rank mapping helpers and formatting.
/// Bands are fixed at 5 stages per subrank:
///  1..5  Bronze I,   6..10 Bronze II,  11..15 Bronze III,
/// 16..20 Silver I,  21..25 Silver II,  26..30 Silver III,
/// 31..35 Gold I,    36..40 Gold II,    41..45 Gold III.
/// Values outside clamp to the nearest supported subrank.
/// </summary>
public static class RankUtility
{
    #region Constants
    private const int STAGES_PER_SUBRANK = 5;
    private const int MAX_SUBRANK_INDEX = 8; // BronzeI..GoldIII (9 entries)
    #endregion

    #region Public API
    public static RankStage GetRankStageForStageNumber(int stageNumber)
    {
        if (stageNumber <= 0) return RankStage.BronzeI;

        int band = (stageNumber - 1) / STAGES_PER_SUBRANK; // 0..∞
        if (band < 0) band = 0;
        if (band > MAX_SUBRANK_INDEX) band = MAX_SUBRANK_INDEX;

        return (RankStage)band;
    }

    public static RankMajor GetMajor(RankStage subrank)
    {
        switch (subrank)
        {
            case RankStage.BronzeI:
            case RankStage.BronzeII:
            case RankStage.BronzeIII:
                return RankMajor.Bronze;

            case RankStage.SilverI:
            case RankStage.SilverII:
            case RankStage.SilverIII:
                return RankMajor.Silver;

            default:
                return RankMajor.Gold;
        }
    }

    public static string Format(RankStage subrank)
    {
        switch (subrank)
        {
            case RankStage.BronzeI: return "Bronze I";
            case RankStage.BronzeII: return "Bronze II";
            case RankStage.BronzeIII: return "Bronze III";
            case RankStage.SilverI: return "Silver I";
            case RankStage.SilverII: return "Silver II";
            case RankStage.SilverIII: return "Silver III";
            case RankStage.GoldI: return "Gold I";
            case RankStage.GoldII: return "Gold II";
            case RankStage.GoldIII: return "Gold III";
            default: return "Bronze I";
        }
    }
    #endregion
}
