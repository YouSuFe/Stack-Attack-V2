using UnityEngine;
using System;

[DefaultExecutionOrder(-188)]
public class ExpService : MonoBehaviour
{
    #region Constants
    private const string PREF_TOTAL_EXP = "EXP_Total";
    #endregion

    #region Singleton
    public static ExpService Instance { get; private set; }
    #endregion

    #region Serialized
    [SerializeField, Tooltip("Nonlinear EXP thresholds asset.")]
    private ExpCurve expCurve;

    [SerializeField, Tooltip("Starting EXP if no save exists.")]
    private int startingTotalExp = 0;
    #endregion

    #region State / Events
    private int totalExp;
    public event Action<int, int> OnExpChanged; // (level, totalExp)
    #endregion

    #region Properties
    public int TotalExp => totalExp;
    public int CurrentLevel => ComputeLevelFromTotal(totalExp);

    /// <summary>EXP total at the start of the current level.</summary>
    public int CurrentLevelStartExp => expCurve ? expCurve.GetCumulativeForLevel(CurrentLevel) : 0;

    /// <summary>EXP total required for the NEXT level (0 if at cap).</summary>
    public int NextLevelTotalExp
    {
        get
        {
            if (expCurve == null) return 0;
            int nextLevel = Mathf.Min(CurrentLevel + 1, expCurve.MaxLevel);
            if (nextLevel == CurrentLevel) return 0; // at cap
            return expCurve.GetCumulativeForLevel(nextLevel);
        }
    }

    /// <summary>EXP already filled in the current level band.</summary>
    public int ExpIntoLevel => Mathf.Max(0, TotalExp - CurrentLevelStartExp);

    /// <summary>EXP needed to reach next level from current progress (0 if at cap).</summary>
    public int ExpToNext => Mathf.Max(0, NextLevelTotalExp > 0 ? NextLevelTotalExp - TotalExp : 0);
    #endregion

    #region Unity
    private void Awake()
    {
        if (Instance != this && Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Load();
    }

    private void OnApplicationQuit() => Save();
    #endregion

    #region Public API
    public void AddExp(int amount, out bool leveledUp, out int levelsGained)
    {
        leveledUp = false; levelsGained = 0;
        if (amount <= 0) return;

        int levelBefore = CurrentLevel;
        totalExp = Mathf.Max(0, totalExp + amount);
        int levelAfter = CurrentLevel;

        if (levelAfter > levelBefore)
        {
            leveledUp = true;
            levelsGained = levelAfter - levelBefore;
        }

        OnExpChanged?.Invoke(CurrentLevel, totalExp);
        Save();
    }

    public void SetTotalExp(int amount)
    {
        totalExp = Mathf.Max(0, amount);
        OnExpChanged?.Invoke(CurrentLevel, totalExp);
        Save();
    }
    #endregion

    #region Helpers
    private int ComputeLevelFromTotal(int total)
    {
        if (expCurve == null || expCurve.cumulativeToLevel == null || expCurve.cumulativeToLevel.Length == 0)
            return 1;

        // Find first threshold strictly greater than total ⇒ current level is its index+1
        var thresholds = expCurve.cumulativeToLevel;
        for (int i = 0; i < thresholds.Length; i++)
        {
            if (total < thresholds[i]) return i + 1;
        }
        // Past last threshold ⇒ max level
        return expCurve.MaxLevel;
    }

    private void Save()
    {
        PlayerPrefs.SetInt(PREF_TOTAL_EXP, Mathf.Max(0, totalExp));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        totalExp = PlayerPrefs.GetInt(PREF_TOTAL_EXP, startingTotalExp);
        OnExpChanged?.Invoke(CurrentLevel, totalExp);
    }
    #endregion
}
