using UnityEngine;
using System;
using System.Collections.Generic;

[DefaultExecutionOrder(-50)]
public class LevelProgressService : MonoBehaviour
{
    #region Constants
    private const string PREF_PREFIX = "LVL_PROGRESS_";
    #endregion

    #region Private Fields
    [SerializeField, Tooltip("If true, loads all level best-progress values on Awake.")]
    private bool loadOnAwake = true;

    private readonly Dictionary<int, float> bestProgressByLevel = new Dictionary<int, float>();
    #endregion

    #region Events
    public event Action<int, float> OnProgressChanged;
    #endregion

    #region Public Properties
    public static LevelProgressService Instance { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (loadOnAwake) PreloadFromPrefs();
    }
    #endregion

    #region Public API
    public float GetBestProgress(int levelIndex)
    {
        if (bestProgressByLevel.TryGetValue(levelIndex, out var value))
            return Mathf.Clamp01(value);

        var loaded = PlayerPrefs.GetFloat(Key(levelIndex), 0f);
        bestProgressByLevel[levelIndex] = Mathf.Clamp01(loaded);
        return bestProgressByLevel[levelIndex];
    }

    public void ReportProgress(int levelIndex, float progress01)
    {
        progress01 = Mathf.Clamp01(progress01);

        if (bestProgressByLevel.TryGetValue(levelIndex, out var current) && current >= progress01)
            return;

        bestProgressByLevel[levelIndex] = progress01;
        PlayerPrefs.SetFloat(Key(levelIndex), progress01);
        OnProgressChanged?.Invoke(levelIndex, progress01);
    }
    #endregion

    #region Helpers
    private static string Key(int index) => $"{PREF_PREFIX}{index}";
    private void PreloadFromPrefs()
    {
        // Lazy: nothing to iterate unless you know total level count.
        // Intentionally left empty; values load on demand.
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Reset All Progress (Danger)")]
    private void Debug_ResetAll()
    {
        bestProgressByLevel.Clear();
        // Optionally iterate known keys if you have a catalog; otherwise manual cleanup.
        Debug.Log("Best progress cleared in memory. PlayerPrefs keys remain unless removed manually.");
    }
#endif
}
