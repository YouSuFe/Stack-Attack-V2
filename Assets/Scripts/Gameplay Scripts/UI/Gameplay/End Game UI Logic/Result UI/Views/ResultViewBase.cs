using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public enum RewardType
{
    None = 0,
    Coin = 1,
    Exp = 2
}

/// <summary>
/// Base class for Result views (Success/Failure).
/// Handles common sequencing: background fade, staggered element reveals,
/// reward card spawning with pop animation, and unscaled-time option.
/// Derived classes only define references + the reveal order,
/// and optionally override OnBeforeSequence() for per-view setup.
/// </summary>
[DisallowMultipleComponent]
public abstract class ResultViewBase : MonoBehaviour
{
    #region Types
    [Serializable]
    public struct RewardEntry
    {
        public RewardType rewardType;
        public int amount;
        public Sprite icon;
    }

    #endregion

    #region Serialized - Common
    [Header("Common - Reward Area")]
    [SerializeField, Tooltip("Parent with GridLayout + ContentSizeFitter for reward cards.")]
    protected RectTransform rewardsGridRoot;

    [SerializeField, Tooltip("Prefab with RewardCardUI component (icon + amount).")]
    protected RewardCardUI rewardCardPrefab;

    [Header("Timing")]
    [SerializeField, Range(0f, 2f), Tooltip("Background fade-in duration.")]
    protected float backgroundFadeDuration = 0.2f;

    [SerializeField, Range(0f, 3f), Tooltip("Delay after background before showing the rest.")]
    protected float delayAfterBackground = 1.0f;

    [SerializeField, Range(0f, 1f), Tooltip("Stagger between each element's reveal.")]
    protected float elementStagger = 0.25f;

    [SerializeField, Range(0f, 3f), Tooltip("Delay between each reward card spawn.")]
    protected float rewardStagger = 0.5f;

    [Header("Animation")]
    [SerializeField, Range(0f, 1f), Tooltip("Fade duration per element.")]
    protected float elementFadeDuration = 0.15f;

    [SerializeField, Range(0.5f, 2f), Tooltip("Pop overshoot scale (1 = none).")]
    protected float popOvershootScale = 1.08f;

    [SerializeField, Range(0f, 0.6f), Tooltip("Pop animation duration per element.")]
    protected float popDuration = 0.12f;

    [Header("Audio")]
    [SerializeField, Tooltip("Played when this result view sequence starts (success/failure fanfare).")]
    protected SoundData viewShowSound;

    [SerializeField, Tooltip("Played each time a reward card is spawned.")]
    protected SoundData rewardCardSpawnSound;

    [Header("Behavior")]
    [SerializeField, Tooltip("Reset all elements hidden on Awake().")]
    protected bool resetHiddenOnAwake = true;

    [SerializeField, Tooltip("If true, uses unscaled time so UI animates even if Time.timeScale == 0.")]
    protected bool useUnscaledTime = true;
    #endregion

    #region Internals
    private Coroutine playRoutine;
    #endregion

    #region Unity
    protected virtual void Awake()
    {
        EnsureAllCanvasGroups();
        if (resetHiddenOnAwake)
            InstantHideAll();
    }
    #endregion

    #region Public API
    /// <summary>Starts the full sequence for this result view.</summary>
    public void PlaySequence(IEnumerable<RewardEntry> rewards)
    {
        Debug.Log($"[{GetType().Name}] PlaySequence() called");

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
        playRoutine = StartCoroutine(DoPlaySequence(rewards));
    }

    /// <summary>Destroys all current reward cards.</summary>
    public void ClearRewards()
    {
        if (rewardsGridRoot == null) return;
        for (int i = rewardsGridRoot.childCount - 1; i >= 0; i--)
            Destroy(rewardsGridRoot.GetChild(i).gameObject);
    }
    #endregion

    #region Core Sequence
    private IEnumerator DoPlaySequence(IEnumerable<RewardEntry> rewards)
    {
        Debug.Log($"[{GetType().Name}] DoPlaySequence() started");

        if (viewShowSound != null)
            SoundUtils.Play2D(viewShowSound);

        InstantHideAll();
        ClearRewards();

        // Per-view setup hook (e.g., Failure configuring "REACHED X%")
        OnBeforeSequence();

        // 1) Background
        var bg = GetBackgroundCanvasGroup();
        if (bg != null)
            yield return StartCoroutine(FadeInWithPop(bg, backgroundFadeDuration));

        // 1b) Delay after background
        if (delayAfterBackground > 0f)
        {
            if (useUnscaledTime) yield return new WaitForSecondsRealtime(delayAfterBackground);
            else yield return new WaitForSeconds(delayAfterBackground);
        }

        // 2) Reveal elements (top → bottom)
        var ordered = BuildSequenceGroups();
        if (ordered != null && ordered.Length > 0)
            yield return StartCoroutine(RevealInOrder(ordered, elementStagger));

        // 3) Rewards
        if (rewards != null)
        {
            foreach (var entry in rewards)
            {
                SpawnCard(entry);
                if (rewardStagger > 0f)
                {
                    if (useUnscaledTime) yield return new WaitForSecondsRealtime(rewardStagger);
                    else yield return new WaitForSeconds(rewardStagger);
                }
            }
        }

        Debug.Log($"[{GetType().Name}] DoPlaySequence() finished");

        playRoutine = null;
    }
    #endregion

    #region Abstract / Virtual (to implement per view)
    /// <summary>Return background CanvasGroup (or null if not used).</summary>
    protected abstract CanvasGroup GetBackgroundCanvasGroup();

    /// <summary>Return CanvasGroups in the reveal order (top → bottom).</summary>
    protected abstract CanvasGroup[] BuildSequenceGroups();

    /// <summary>Ensure all referenced elements have CanvasGroups (cache them).</summary>
    protected abstract void EnsureAllCanvasGroups();

    /// <summary>Optional per-view setup executed before the sequence starts.</summary>
    protected virtual void OnBeforeSequence() { }
    #endregion

    #region Shared Helpers
    protected static CanvasGroup EnsureCg(GameObject go)
    {
        if (go == null) return null;
        if (!go.TryGetComponent(out CanvasGroup cg))
            cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    protected static void HideCg(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.transform.localScale = Vector3.one;
    }

    protected void InstantHideAll()
    {
        var bg = GetBackgroundCanvasGroup();
        if (bg != null) HideCg(bg);

        var groups = BuildSequenceGroups();
        if (groups == null) return;
        for (int i = 0; i < groups.Length; i++)
            HideCg(groups[i]);
    }

    protected IEnumerator FadeInWithPop(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;

        cg.gameObject.SetActive(true);
        cg.blocksRaycasts = true;
        cg.interactable = false;
        cg.alpha = 0f;
        cg.transform.localScale = Vector3.one * 0.95f;

        float t = 0f;
        while (t < duration)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Lerp(0f, 1f, u);

            float popU = Mathf.Clamp01(t / popDuration);
            float scale = Mathf.Lerp(0.95f, popOvershootScale, popU);
            cg.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        cg.alpha = 1f;
        cg.transform.localScale = Vector3.one;
        cg.interactable = true;
    }

    protected IEnumerator RevealInOrder(CanvasGroup[] groups, float stagger)
    {
        foreach (var cg in groups)
        {
            if (cg != null)
                StartCoroutine(FadeInElement(cg, elementFadeDuration));

            if (stagger > 0f)
            {
                if (useUnscaledTime) yield return new WaitForSecondsRealtime(stagger);
                else yield return new WaitForSeconds(stagger);
            }
            else
            {
                yield return null;
            }
        }
    }

    protected IEnumerator FadeInElement(CanvasGroup cg, float duration)
    {
        if (cg == null) yield break;

        cg.alpha = 0f;
        cg.interactable = false;
        cg.gameObject.SetActive(true);
        cg.blocksRaycasts = true;

        float t = 0f;
        cg.transform.localScale = Vector3.one * 0.95f;

        while (t < duration)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Lerp(0f, 1f, u);

            // ease-out scale
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            cg.transform.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, eased);

            yield return null;
        }

        cg.alpha = 1f;
        cg.interactable = true;
        cg.transform.localScale = Vector3.one;
    }

    protected void SpawnCard(RewardEntry entry)
    {
        if (rewardCardPrefab == null || rewardsGridRoot == null)
        {
            Debug.LogWarning($"[{GetType().Name}] Missing reward prefab or grid root.");
            return;
        }

        var card = Instantiate(rewardCardPrefab, rewardsGridRoot);
        card.Set(entry.icon, entry.amount);

        if (rewardCardSpawnSound != null)
            SoundUtils.Play2D(rewardCardSpawnSound);

        StartCoroutine(CardPop(card.transform));
    }

    protected IEnumerator CardPop(Transform tr)
    {
        float t = 0f;
        while (t < popDuration)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float u = Mathf.Clamp01(t / popDuration);
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            tr.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, eased);
            yield return null;
        }
        tr.localScale = Vector3.one;
    }
    #endregion
}
