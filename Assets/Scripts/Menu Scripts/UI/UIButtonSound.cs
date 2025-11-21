using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Universal button click-sound component.
/// Attach this to any UI Button to automatically play a sound on click.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField, Tooltip("Sound played when this button is clicked.")]
    private SoundData clickSound;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(PlaySound);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(PlaySound);
    }

    private void PlaySound()
    {
        if (clickSound != null)
            SoundUtils.Play2D(clickSound);
    }
}
