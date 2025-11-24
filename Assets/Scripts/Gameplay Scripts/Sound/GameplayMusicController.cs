using UnityEngine;

public class GameplayMusicController : MonoBehaviour
{
    private void Start()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayGameplayMusic();
        }
    }
}