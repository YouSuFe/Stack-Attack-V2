// PlayerDragMover.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDragMover : MonoBehaviour, IStoppable
{
    [Header("Dependencies")]
    [SerializeField] private Camera gameplayCamera;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20f;           // units/sec toward target X
    [SerializeField] private Vector2 xBounds = new Vector2(-3.5f, 3.5f);

    private Rigidbody2D playerRigidBody2D;
    private bool isDragging;
    private bool isStopped;
    private float startOffsetX;
    private float targetX;

    private void Awake()
    {
        playerRigidBody2D = GetComponent<Rigidbody2D>();
        if (gameplayCamera == null) gameplayCamera = Camera.main;

        // Recommended Rigidbody2D setup for kinematic "character controller" style motion
        playerRigidBody2D.bodyType = RigidbodyType2D.Kinematic;
        playerRigidBody2D.interpolation = RigidbodyInterpolation2D.Interpolate;

        targetX = transform.position.x;
    }

    private void OnEnable()
    {
        PauseManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        PauseManager.Instance?.Unregister(this);
    }

    private void FixedUpdate()
    {
        if (isStopped) return; // freeze gameplay motion during upgrade UI

        // Smoothly move horizontally toward targetX using physics-friendly motion
        Vector2 position = playerRigidBody2D.position;
        float maxStep = moveSpeed * Time.fixedDeltaTime;
        float newX = Mathf.MoveTowards(position.x, targetX, maxStep);
        playerRigidBody2D.MovePosition(new Vector2(newX, position.y));
    }

    /// <summary>Begin a drag at the given screen position.</summary>
    public void BeginDrag(Vector2 screenPosition)
    {
        if (isStopped) return;

        isDragging = true;
        float pointerWorldX = ScreenToWorldX(screenPosition);
        startOffsetX = playerRigidBody2D.position.x - pointerWorldX; // preserve initial “grip” offset
        targetX = Mathf.Clamp(pointerWorldX + startOffsetX, xBounds.x, xBounds.y);
    }

    /// <summary>Update drag (while the finger/mouse is held).</summary>
    public void UpdateDrag(Vector2 screenPosition)
    {
        if (!isDragging || isStopped) return;

        float pointerWorldX = ScreenToWorldX(screenPosition);
        float desiredX = pointerWorldX + startOffsetX;
        targetX = Mathf.Clamp(desiredX, xBounds.x, xBounds.y);
    }

    /// <summary>End the drag.</summary>
    public void EndDrag(Vector2 screenPosition)
    {
        isDragging = false;
    }

    /// <summary>Force end (used by soft pause).</summary>
    public void ForceEndDrag()
    {
        isDragging = false;
    }

    // ------------------------
    // Utilities
    // ------------------------

    private float ScreenToWorldX(Vector2 screenPos)
    {
        if (gameplayCamera == null) gameplayCamera = Camera.main;

        // For an orthographic camera, use the Z distance from camera to world plane.
        // For a perspective camera, we use absolute Z to project from screen onto a plane in front of the camera.
        float zDistance = gameplayCamera.orthographic
            ? -gameplayCamera.transform.position.z
            : Mathf.Abs(gameplayCamera.transform.position.z);

        Vector3 world = gameplayCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDistance));
        return world.x;
    }

    public void SetBounds(Vector2 newBounds) => xBounds = newBounds;
    public void SetMoveSpeed(float newSpeed) => moveSpeed = newSpeed;

    // ------------------------
    // IStoppable
    // ------------------------

    public void OnStopGameplay()
    {
        isStopped = true;
        isDragging = false;
    }

    public void OnResumeGameplay()
    {
        isStopped = false;
    }
}
