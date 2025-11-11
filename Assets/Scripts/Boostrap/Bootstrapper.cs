using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using PixeLadder.EasyTransition;

public class Bootstrapper : MonoBehaviour
{
    #region Inspector
    [SerializeField, Tooltip("Optional splash delay before transitioning to the Menu.")]
    private float splashDelaySeconds = 0.0f;

    [SerializeField, Tooltip("Log basic bootstrap steps in development.")]
    private bool enableVerboseLogs = false;
    #endregion

    #region Unity
    private void Start()
    {
        StartCoroutine(BootstrapAndGoToMenu());
    }
    #endregion

    #region Flow
    private IEnumerator BootstrapAndGoToMenu()
    {
        // Give one frame so all services in GameRuntime complete Awake() and load saves.
        yield return null;

        if (splashDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(splashDelaySeconds);

        if (enableVerboseLogs)
            Debug.Log("[Bootstrapper] Transitioning to Menu via SceneTransitioner.");

        // Use your transition system (fade, etc.)
        SceneTransitioner.Instance.LoadScene(SceneNames.MainMenu);
    }
    #endregion
}
