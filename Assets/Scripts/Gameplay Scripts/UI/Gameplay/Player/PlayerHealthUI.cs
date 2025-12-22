using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Minimal health UI: instantiates/destroys heart icons to match PlayerHealth.CurrentHearts.
/// No pooling, just straightforward add/remove.
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    #region Private Fields
    [Header("References")]
    [Tooltip("PlayerHealth to observe. If left null, will try to find one in the scene on Awake.")]
    [SerializeField] private PlayerHealth playerHealth;

    [Tooltip("Parent RectTransform where heart icons will be instantiated.")]
    [SerializeField] private RectTransform heartsContainer;

    [Tooltip("Prefab with an Image component using your heart sprite.")]
    [SerializeField] private GameObject heartPrefab;

    // Currently displayed hearts (left to right)
    private readonly List<GameObject> activeHearts = new List<GameObject>();

    private int lastDrawnHeartCount = -1;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("[HealthUIHeartsSimple] PlayerHealth not found. Please assign in Inspector.");
        }

        if (heartsContainer == null)
        {
            Debug.LogError("[HealthUIHeartsSimple] HeartsContainer is not assigned.");
        }

        if (heartPrefab == null)
        {
            Debug.LogError("[HealthUIHeartsSimple] HeartPrefab is not assigned.");
        }
        else if (!heartPrefab.TryGetComponent<Image>(out _))
        {
            Debug.LogWarning("[HealthUIHeartsSimple] HeartPrefab has no Image component. Add one to show the sprite.");
        }
    }

    private void Start()
    {
        RebuildTo(playerHealth.CurrentHearts);
    }

    private void OnEnable()
    {
        Subscribe();
        RebuildTo(playerHealth != null ? playerHealth.CurrentHearts : 0);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }
    #endregion

    #region Subscriptions
    private void Subscribe()
    {
        if (playerHealth == null) return;

        playerHealth.OnDamaged += HandleDamaged;
        playerHealth.OnHealed += HandleHealed;
        playerHealth.OnDied += HandleDied;
    }

    private void Unsubscribe()
    {
        if (playerHealth == null) return;

        playerHealth.OnDamaged -= HandleDamaged;
        playerHealth.OnHealed -= HandleHealed;
        playerHealth.OnDied -= HandleDied;
    }
    #endregion

    #region Event Handlers
    private void HandleDamaged(int currentHearts, int damageAmount, GameObject _)
    {
        RebuildTo(currentHearts);
    }

    private void HandleHealed(int currentHearts, int healAmount)
    {
        RebuildTo(currentHearts + healAmount);
    }

    private void HandleDied()
    {
        RebuildTo(0);
    }
    #endregion

    #region Build / Destroy
    /// <summary>
    /// Match the UI to targetCount by instantiating or destroying icons.
    /// </summary>
    private void RebuildTo(int targetCount)
    {
        if (heartsContainer == null || heartPrefab == null) return;
        if (lastDrawnHeartCount == targetCount) return; // no churn
        lastDrawnHeartCount = targetCount;

        // Add hearts if needed
        while (activeHearts.Count < targetCount)
        {
            Debug.LogWarning($"{activeHearts.Count} have active hearths. We will add until {targetCount}");
            GameObject heart = Instantiate(heartPrefab, heartsContainer);
            heart.transform.localScale = Vector3.one;
            heart.SetActive(true);
            activeHearts.Add(heart);
        }

        // Remove hearts if too many
        while (activeHearts.Count > targetCount)
        {
            int lastIndex = activeHearts.Count - 1;
            GameObject heart = activeHearts[lastIndex];
            activeHearts.RemoveAt(lastIndex);
            if (heart != null) Destroy(heart);
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Force a refresh (useful after scene load).
    /// </summary>
    public void RefreshNow()
    {
        int count = playerHealth != null ? playerHealth.CurrentHearts : 0;
        RebuildTo(count);
    }

    /// <summary>
    /// Rebind to a different PlayerHealth at runtime.
    /// </summary>
    public void Bind(PlayerHealth newHealth)
    {
        if (playerHealth == newHealth) return;

        Unsubscribe();
        playerHealth = newHealth;
        Subscribe();
        RefreshNow();
    }
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Rebuild From Player")]
    private void DebugRebuild()
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("[HealthUIHeartsSimple] No PlayerHealth assigned.");
            return;
        }
        RebuildTo(playerHealth.CurrentHearts);
        Debug.Log($"[HealthUIHeartsSimple] Rebuilt to {playerHealth.CurrentHearts} hearts.");
    }

    [ContextMenu("Debug/Clear")]
    private void DebugClear()
    {
        RebuildTo(0);
        Debug.Log("[HealthUIHeartsSimple] Cleared all hearts.");
    }
#endif
}

