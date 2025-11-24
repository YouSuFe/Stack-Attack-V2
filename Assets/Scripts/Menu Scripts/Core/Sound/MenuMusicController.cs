using UnityEngine;

/// <summary>
/// Ensures menu music is playing when this scene/UI is active.
/// </summary>
public class MenuMusicController : MonoBehaviour
{
    private void Start()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.PlayMenuMusic();
        }
    }
}
