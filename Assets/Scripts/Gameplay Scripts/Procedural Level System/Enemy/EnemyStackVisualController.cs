using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a stack of visual segments (e.g., octagon sprites) to represent enemy health.
/// Supports two modes:
/// 1) maxHealth <= lowHealthThreshold: 1 HP = 1 visual segment.
/// 2) maxHealth > lowHealthThreshold: health is divided into chunks across all visuals.
/// 
/// Also coordinates with EnemyHealthTextPositioner so the health text sits on top of the
/// highest active visual.
/// </summary>
[DisallowMultipleComponent]
public class EnemyStackVisualController : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [SerializeField, Tooltip("EnemyHealth component for this enemy. If not set, will try to auto-find one on this object or in children.")]
    private EnemyHealth enemyHealth;

    [SerializeField, Tooltip("Root transform that contains all stacked visual segments as children (bottom to top).")]
    private Transform visualRoot;

    [SerializeField, Tooltip("Optional explicit list of stacked visuals, ordered bottom (index 0) to top. If empty and Auto Populate is true, this will be filled from visualRoot children sorted by local Y.")]
    private List<Transform> visualSegments = new List<Transform>();

    [SerializeField, Tooltip("Optional positioner that will move the health text above the top active visual.")]
    private EnemyHealthTextPositioner healthTextPositioner;

    [Header("Visual Settings")]
    [SerializeField, Tooltip("Maximum number of visual segments to use. Extra entries in visualSegments will be ignored.")]
    private int maxVisuals = 10;

    [SerializeField, Tooltip("If maxHealth is <= this value, each HP is represented by a single visual segment. Above this value uses chunked mode.")]
    private int lowHealthThreshold = 10;

    [SerializeField, Tooltip("If true and visualSegments list is empty, children of visualRoot will be auto-populated and sorted by local Y (bottom to top).")]
    private bool autoPopulateSegments = true;

    [SerializeField, Tooltip("If true, all visual segments will be set inactive on Awake before any health is applied.")]
    private bool deactivateAllOnAwake = true;

    #endregion

    #region Private Fields

    private enum VisualMode
    {
        Unknown = 0,
        Linear = 1,   // 1 HP = 1 visual (for maxHealth <= lowHealthThreshold)
        Chunked = 2   // HP distributed across all visuals (for maxHealth > lowHealthThreshold)
    }

    private VisualMode currentMode = VisualMode.Unknown;
    private int cachedMaxHealth = 0;
    private float chunkSize = 0f;
    private int lastActiveVisualCount = -1;
    private bool isSubscribedToHealth = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeEnemyHealthReference();
        InitializeVisualSegments();

        if (deactivateAllOnAwake)
        {
            SetAllSegmentsActive(false);
        }
    }

    private void OnEnable()
    {
        SubscribeToHealth();
    }

    private void OnDisable()
    {
        UnsubscribeFromHealth();
    }

    #endregion

    #region Initialization Helpers

    private void InitializeEnemyHealthReference()
    {
        if (enemyHealth == null)
        {
            if (!TryGetComponent(out enemyHealth))
            {
                enemyHealth = GetComponentInChildren<EnemyHealth>();
            }
        }

        if (enemyHealth == null)
        {
            Debug.LogWarning($"[{nameof(EnemyStackVisualController)}] No EnemyHealth found for '{name}'. Visuals will not react to health.", this);
        }
    }

    private void InitializeVisualSegments()
    {
        if (visualRoot == null)
        {
            Debug.LogWarning($"[{nameof(EnemyStackVisualController)}] Visual root not assigned on '{name}'.", this);
            return;
        }

        if (autoPopulateSegments && visualSegments.Count == 0)
        {
            visualSegments.Clear();

            // Collect all direct children (you can change this to GetComponentsInChildren
            // if you nest visuals deeper).
            for (int i = 0; i < visualRoot.childCount; i++)
            {
                var child = visualRoot.GetChild(i);
                visualSegments.Add(child);
            }

            // Sort bottom (lowest local Y) to top (highest local Y)
            visualSegments.Sort((a, b) => a.localPosition.y.CompareTo(b.localPosition.y));
        }

        if (visualSegments.Count == 0)
        {
            Debug.LogWarning($"[{nameof(EnemyStackVisualController)}] No visual segments configured for '{name}'.", this);
        }
    }

    private void SubscribeToHealth()
    {
        if (enemyHealth == null || isSubscribedToHealth)
            return;

        enemyHealth.OnHealthChanged += HandleHealthChanged;
        isSubscribedToHealth = true;
    }

    private void UnsubscribeFromHealth()
    {
        if (enemyHealth == null || !isSubscribedToHealth)
            return;

        enemyHealth.OnHealthChanged -= HandleHealthChanged;
        isSubscribedToHealth = false;
    }

    #endregion

    #region Health Handling

    /// <summary>
    /// Callback from EnemyHealth whenever health changes (including initialization).
    /// currentHealth and maxHealth come from EnemyHealth.
    /// </summary>
    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (visualSegments == null || visualSegments.Count == 0)
            return;

        if (maxHealth <= 0)
        {
            // Nothing to show.
            ApplyActiveVisualCount(0);
            return;
        }

        // Initialize / update mode if needed.
        if (currentMode == VisualMode.Unknown || cachedMaxHealth != maxHealth)
        {
            cachedMaxHealth = maxHealth;
            DetermineVisualModeAndChunkSize();
        }

        int visualCapacity = Mathf.Clamp(visualSegments.Count, 0, maxVisuals);
        if (visualCapacity <= 0)
        {
            return;
        }

        int activeVisualCount = ComputeActiveVisualCount(currentHealth, visualCapacity);

        ApplyActiveVisualCount(activeVisualCount);
    }

    private void DetermineVisualModeAndChunkSize()
    {
        int visualCapacity = Mathf.Clamp(visualSegments.Count, 0, maxVisuals);
        if (visualCapacity <= 0)
        {
            currentMode = VisualMode.Unknown;
            chunkSize = 0f;
            return;
        }

        if (cachedMaxHealth <= lowHealthThreshold)
        {
            currentMode = VisualMode.Linear;
            chunkSize = 0f;
        }
        else
        {
            currentMode = VisualMode.Chunked;
            chunkSize = (float)cachedMaxHealth / visualCapacity;
            if (chunkSize <= 0f)
            {
                chunkSize = 1f;
            }
        }
    }

    private int ComputeActiveVisualCount(int currentHealth, int visualCapacity)
    {
        currentHealth = Mathf.Max(currentHealth, 0);

        if (currentMode == VisualMode.Linear)
        {
            // 1 HP = 1 visual (clamped).
            return Mathf.Clamp(currentHealth, 0, visualCapacity);
        }

        if (currentMode == VisualMode.Chunked)
        {
            if (currentHealth <= 0)
                return 0;

            // e.g. max=110, capacity=10 -> chunkSize=11
            // 110 -> ceil(110/11)=10 visuals
            // 99  -> ceil( 99/11)= 9 visuals
            float value = currentHealth / Mathf.Max(chunkSize, 0.0001f);
            int count = Mathf.CeilToInt(value);
            return Mathf.Clamp(count, 0, visualCapacity);
        }

        // Fallback: show nothing if mode unknown.
        return 0;
    }

    private void ApplyActiveVisualCount(int activeVisualCount)
    {
        activeVisualCount = Mathf.Max(activeVisualCount, 0);

        // Avoid out-of-range if inspector list is longer than maxVisuals.
        int visualCapacity = Mathf.Clamp(visualSegments.Count, 0, maxVisuals);

        // Clamp to capacity.
        if (activeVisualCount > visualCapacity)
        {
            activeVisualCount = visualCapacity;
        }

        // Activate / deactivate visuals.
        for (int i = 0; i < visualSegments.Count; i++)
        {
            Transform segment = visualSegments[i];
            if (segment == null)
                continue;

            bool shouldBeActive = (i < activeVisualCount) && (i < visualCapacity);
            if (segment.gameObject.activeSelf != shouldBeActive)
            {
                segment.gameObject.SetActive(shouldBeActive);
            }
        }

        lastActiveVisualCount = activeVisualCount;

        // Update health text position.
        Transform topVisual = null;
        if (activeVisualCount > 0 && activeVisualCount <= visualSegments.Count)
        {
            topVisual = visualSegments[activeVisualCount - 1];
        }

        if (healthTextPositioner != null)
        {
            healthTextPositioner.PositionAbove(topVisual);
        }
    }

    #endregion

    #region Utility

    private void SetAllSegmentsActive(bool isActive)
    {
        if (visualSegments == null)
            return;

        for (int i = 0; i < visualSegments.Count; i++)
        {
            if (visualSegments[i] != null)
            {
                visualSegments[i].gameObject.SetActive(isActive);
            }
        }
    }

    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Print Visual Setup")]
    private void DebugPrintVisualSetup()
    {
        Debug.Log($"[{nameof(EnemyStackVisualController)}] '{name}' visuals: " +
                  $"{visualSegments.Count} segments, mode={currentMode}, cachedMaxHealth={cachedMaxHealth}, chunkSize={chunkSize}",
                  this);
    }
#endif
}
