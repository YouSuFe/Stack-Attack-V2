using UnityEngine;
/// <summary>
/// Positions a world-space health text (Canvas/TMP) above the top active visual segment.
/// Called by EnemyStackVisualController whenever the number of active visuals changes.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealthTextPositioner : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [SerializeField, Tooltip("Transform of the health text root (e.g., world-space Canvas or TMP object). If null, will try to auto-find.")]
    private Transform textRoot;

    [Header("Positioning")]
    [SerializeField, Tooltip("Extra world-space Y offset applied above the top visual segment.")]
    private float extraHeightOffset = 0.05f;

    [SerializeField, Tooltip("If true, the health text will be hidden when there are no active visuals.")]
    private bool hideWhenNoVisuals = false;

    #endregion

    #region Private Fields

    private bool hasInitializedDefaultPosition = false;
    private Vector3 defaultWorldPosition;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeTextRoot();
        CacheDefaultPosition();
    }

    #endregion

    #region Initialization Helpers

    private void InitializeTextRoot()
    {
        if (textRoot != null)
            return;

        // If this script is on the Canvas/Text object itself, use self.
        textRoot = transform;

        // If you want, you can extend this to search for EnemyHealthText or TMP components,
        // but keeping it simple and explicit is usually more reliable in production.
    }

    private void CacheDefaultPosition()
    {
        if (textRoot == null)
            return;

        defaultWorldPosition = textRoot.position;
        hasInitializedDefaultPosition = true;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Positions the text above the given top visual.
    /// If topVisual is null:
    /// - If hideWhenNoVisuals is true, hides the text.
    /// - Otherwise, keeps or restores the default world position.
    /// </summary>
    /// <param name="topVisual">Topmost active visual segment, or null if none.</param>
    public void PositionAbove(Transform topVisual)
    {
        if (textRoot == null)
            return;

        if (topVisual == null)
        {
            if (hideWhenNoVisuals)
            {
                if (textRoot.gameObject.activeSelf)
                {
                    textRoot.gameObject.SetActive(false);
                }
            }
            else if (hasInitializedDefaultPosition)
            {
                textRoot.position = defaultWorldPosition;
            }

            return;
        }

        // Ensure text is visible when we have a top visual.
        if (hideWhenNoVisuals && !textRoot.gameObject.activeSelf)
        {
            textRoot.gameObject.SetActive(true);
        }

        // Place the text just above the top visual in world space.
        Vector3 worldPos = topVisual.position;
        worldPos.y += extraHeightOffset;

        // Keep original X/Z if you want to lock horizontal position.
        // For now, follow the visual exactly.
        textRoot.position = worldPos;
    }

    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug/Log Text Position")]
    private void DebugLogTextPosition()
    {
        if (textRoot != null)
        {
            Debug.Log($"[{nameof(EnemyHealthTextPositioner)}] '{name}' text world position = {textRoot.position}", textRoot);
        }
        else
        {
            Debug.LogWarning($"[{nameof(EnemyHealthTextPositioner)}] '{name}' has no textRoot assigned.", this);
        }
    }
#endif
}
