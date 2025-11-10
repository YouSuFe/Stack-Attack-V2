using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MainMenu";

    private void Start() { StartCoroutine(Boot()); }

    private IEnumerator Boot()
    {
        yield return null; // let services Awake and load PlayerPrefs
        SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }
}
