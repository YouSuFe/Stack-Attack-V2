using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDragMover : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Camera gameplayCamera;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20f;                 // units per second toward target
    [SerializeField] private Vector2 xBounds = new Vector2(-3.5f, 3.5f);

    private Rigidbody2D rigidbody2D;
    private bool isDragging;
    private float startOffsetX;
    private float targetX;

    private void Awake()
    {
        rigidbody2D = GetComponent<Rigidbody2D>();
        if (gameplayCamera == null) gameplayCamera = Camera.main;

        // Ensure recommended RB2D setup (can also be set in Inspector)
        rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
        targetX = transform.position.x;
    }

    private void OnEnable()
    {
        if (inputReader == null)
        {
            Debug.LogError("PlayerDragMover: inputReader is not assigned.");
            return;
        }

        inputReader.EnableInput();
        inputReader.OnDragStarted += HandleDragStarted;
        inputReader.OnDrag += HandleDrag;
        inputReader.OnDragEnded += HandleDragEnded;
    }

    private void OnDisable()
    {
        if (inputReader == null) return;
        inputReader.OnDragStarted -= HandleDragStarted;
        inputReader.OnDrag -= HandleDrag;
        inputReader.OnDragEnded -= HandleDragEnded;
    }

    private void FixedUpdate()
    {
        // Move horizontally toward targetX using physics-friendly motion
        Vector2 position = rigidbody2D.position;
        float maxStep = moveSpeed * Time.fixedDeltaTime;
        float newX = Mathf.MoveTowards(position.x, targetX, maxStep);
        rigidbody2D.MovePosition(new Vector2(newX, position.y));
    }

    private void HandleDragStarted(Vector2 screenPosition)
    {
        isDragging = true;
        float pointerWorldX = ScreenToWorldX(screenPosition);
        startOffsetX = rigidbody2D.position.x - pointerWorldX; // preserve grip offset
        targetX = Mathf.Clamp(pointerWorldX + startOffsetX, xBounds.x, xBounds.y);
    }

    private void HandleDrag(Vector2 screenPosition)
    {
        if (!isDragging) return;

        float pointerWorldX = ScreenToWorldX(screenPosition);
        float desiredX = pointerWorldX + startOffsetX;
        targetX = Mathf.Clamp(desiredX, xBounds.x, xBounds.y);
    }

    private void HandleDragEnded(Vector2 screenPosition)
    {
        isDragging = false;
    }

    private float ScreenToWorldX(Vector2 screenPos)
    {
        if (gameplayCamera == null) gameplayCamera = Camera.main;

        float zDistance = gameplayCamera.orthographic
            ? -gameplayCamera.transform.position.z
            : Mathf.Abs(gameplayCamera.transform.position.z);

        Vector3 world = gameplayCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDistance));
        return world.x;
    }

    public void SetBounds(Vector2 newBounds) => xBounds = newBounds;
    public void SetMoveSpeed(float newSpeed) => moveSpeed = newSpeed;
}
