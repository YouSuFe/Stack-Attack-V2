using UnityEngine;

[DisallowMultipleComponent]
public class PinataDirector : MonoBehaviour
{
    #region Singleton
    private static PinataDirector instance;
    public static PinataDirector Instance
    {
        get
        {
            if (instance == null)
                Debug.LogError("[PinataDirector] No instance in scene.");
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[PinataDirector] Multiple instances detected. Destroying extra.");
            Destroy(gameObject);
            return;
        }
        instance = this;
        if (!targetCamera) targetCamera = Camera.main;

        if (pinataBackdrop) pinataBackdrop.SetActive(false);
        if (damageReceiver) damageReceiver.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
    #endregion

    #region Serialized
    [Header("Positioning")]
    [SerializeField, Tooltip("If assigned, used as exact pinata anchor.")]
    private Transform pinataAnchor;

    [SerializeField, Tooltip("Used if anchor is null. Y = camera top + this offset.")]
    private float cameraTopYOffset = 0.35f;

    [SerializeField] private Camera targetCamera;

    [Header("Presentation")]
    [SerializeField, Tooltip("Backdrop/plate shown during pinata.")]
    private GameObject pinataBackdrop;

    [SerializeField, Tooltip("Damage relay for top hits → PinataMeter.")]
    private PinataDamageReceiver damageReceiver;
    #endregion

    #region Runtime
    private BossStateController activeBoss;
    private PinataMeter activeMeter;
    #endregion

    #region Public API
    public void BeginPinataFor(BossStateController boss, PinataMeter meter)
    {
        if (!boss || !meter)
        {
            Debug.LogWarning("[PinataDirector] BeginPinataFor with null boss/meter.");
            return;
        }

        activeBoss = boss;
        activeMeter = meter;

        Vector3 pos = ComputePinataPosition(boss.transform.position);
        boss.transform.position = pos;

        if (pinataBackdrop) pinataBackdrop.SetActive(true);

        activeMeter.EnablePinata(true);

        if (damageReceiver)
        {
            damageReceiver.BindPinataMeter(activeMeter);
            damageReceiver.gameObject.SetActive(true);
        }
    }

    public void EndPinata()
    {
        if (pinataBackdrop) pinataBackdrop.SetActive(false);
        if (damageReceiver) damageReceiver.gameObject.SetActive(false);

        if (activeMeter) activeMeter.EnablePinata(false);
        activeBoss = null;
        activeMeter = null;
    }
    #endregion

    #region Helpers
    private Vector3 ComputePinataPosition(Vector3 fallbackAroundX)
    {
        if (pinataAnchor) return pinataAnchor.position;

        var cam = targetCamera ? targetCamera : Camera.main;
        if (!cam) return fallbackAroundX;

        float topY = cam.orthographic
            ? cam.transform.position.y + cam.orthographicSize
            : cam.ViewportToWorldPoint(new Vector3(0f, 1f, Mathf.Abs(cam.transform.position.z))).y;

        float x = cam.transform.position.x; // center horizontally
        return new Vector3(x, topY + cameraTopYOffset, fallbackAroundX.z);
    }
    #endregion
}
