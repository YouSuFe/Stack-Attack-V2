using UnityEngine;
using UnityEngine.UI; // Required for Button
using UnityEngine.SceneManagement; // For quitting or scene control if needed
using PixeLadder.EasyTransition;

public class MenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    [Header("Transition Effects")]
    [SerializeField] private TransitionEffect[] fadeEffects;

    private void Awake()
    {
        // Ensure the buttons are assigned in the Inspector
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnStartClicked()
    {
        SceneTransitioner.Instance.LoadScene(SceneNames.GamePlay, fadeEffects[Random.Range(0,fadeEffects.Length)]);
    }

    private void OnQuitClicked()
    {
        // Quit the game
        Debug.Log("Quitting game...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stop play mode in editor
#else
        Application.Quit(); // Quit the built game
#endif
    }
}