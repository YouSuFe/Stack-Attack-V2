using UnityEngine;
/// <summary>
/// Moves this GameObject downward at a constant speed.
/// </summary>
public class MoveDown : MonoBehaviour
{
    [Tooltip("Movement speed in world units per second.")]
    [SerializeField] private float moveSpeed = 2f;

    private void Update()
    {
        if (PauseManager.Instance == null || PauseManager.Instance.IsGameplayStopped)
            return;
        // Move downward at constant speed (frame-rate independent)
        transform.Translate(Vector3.down * moveSpeed * Time.deltaTime, Space.World);
    }
}
