using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameRuntime : MonoBehaviour
{
    public static GameRuntime Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
