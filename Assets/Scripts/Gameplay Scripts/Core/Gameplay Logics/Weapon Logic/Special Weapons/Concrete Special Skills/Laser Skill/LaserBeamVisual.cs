using UnityEngine;

public class LaserBeamVisual : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private LineRenderer lineRenderer;
    #endregion

    #region Public Properties
    public LineRenderer Line => lineRenderer;
    #endregion

    #region Unity Lifecycle
    private void Reset()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
            lineRenderer.useWorldSpace = true;
    }
    #endregion

    #region Public Methods
    public void SetEndpoints(Vector3 start, Vector3 end)
    {
        if (lineRenderer == null) return;
        if (!lineRenderer.useWorldSpace) lineRenderer.useWorldSpace = true;

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    public void SetActive(bool value)
    {
        gameObject.SetActive(value);
    }
    #endregion
}
