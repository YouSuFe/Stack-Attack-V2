using UnityEngine;

public class PlayerReviveController : MonoBehaviour
{
    [SerializeField, Tooltip("Reference to the player's health component.")]
    private PlayerHealth playerHealth;

    [SerializeField, Tooltip("Reference to the DeadCanvasController handling revive UI.")]
    private DeadCanvasController deadCanvas;

    private void Awake()
    {
        if (!playerHealth) TryGetComponent(out playerHealth);
    }

    private void OnEnable()
    {
        if (deadCanvas != null)
            deadCanvas.ReviveRequested += HandleReviveRequested;
    }

    private void OnDisable()
    {
        if (deadCanvas != null)
            deadCanvas.ReviveRequested -= HandleReviveRequested;
    }

    private void HandleReviveRequested()
    {
        playerHealth?.Revive();

        LevelContextBinder.Instance?.ResetOutcomeHandled();
    }
}

