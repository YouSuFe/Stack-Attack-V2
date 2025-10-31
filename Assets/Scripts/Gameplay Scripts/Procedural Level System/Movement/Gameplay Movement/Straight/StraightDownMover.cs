using UnityEngine;

[DisallowMultipleComponent]
public class StraightDownMover : MonoBehaviour, IStageActivatable
{
    #region Inspector
    [Tooltip("Downward speed (units/sec).")]
    [SerializeField, Range(0f, 50f)] private float verticalSpeed = 3f;

    #endregion

    #region Private Fields
    private bool isActive = false;
    #endregion

    #region Public API
    public void SetParameters(float vSpeed) => verticalSpeed = Mathf.Max(0f, vSpeed);
    #endregion

    #region IStageActivatable
    public void PauseMover() => isActive = false;

    public void ArmAtEntry(Vector3 entryWorldPos)
    {
        // No baseline capture needed for straight down.
    }

    public void ResumeMover() => isActive = true;
    #endregion

    private void Update()
    {
        if (!isActive) return;

        if (verticalSpeed > 0f)
            transform.position += Vector3.down * (verticalSpeed * Time.deltaTime);
    }
}
