using UnityEngine;

/// <summary>
/// Switches between two boss visuals depending on the BossStateController.
/// Default visual = active in all states except Pinata.
/// Pinata visual = only active during Pinata state.
/// </summary>
public class BossPinataVisualSwitcher : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField, Tooltip("Reference to the BossStateController on this boss.")]
    private BossStateController bossStateController;

    [SerializeField, Tooltip("Visual shown during all states EXCEPT Pinata.")]
    private GameObject defaultVisual;

    [SerializeField, Tooltip("Visual shown ONLY in Pinata state.")]
    private GameObject pinataVisual;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (bossStateController == null)
            TryGetComponent(out bossStateController);

        // Ensure correct initial state
        SetVisuals(defaultActive: true);
    }

    private void OnEnable()
    {
        if (bossStateController != null)
            bossStateController.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (bossStateController != null)
            bossStateController.OnStateChanged -= HandleStateChanged;
    }
    #endregion

    #region State Handling
    private void HandleStateChanged(BossStateController.BossState newState)
    {
        switch (newState)
        {
            case BossStateController.BossState.Pinata:
            case BossStateController.BossState.Breaking:
            case BossStateController.BossState.End:
                SetVisuals(defaultActive: false);
                break;
            default:
                SetVisuals(defaultActive: true);
                break;
        }
    }
    #endregion

    #region Helpers
    private void SetVisuals(bool defaultActive)
    {
        if (defaultVisual != null)
            defaultVisual.SetActive(defaultActive);

        if (pinataVisual != null)
            pinataVisual.SetActive(!defaultActive);
    }
    #endregion
}
