using UnityEngine;
/// <summary>
/// Central helper utility for playing sounds using the SoundManager + SoundBuilder system.
/// Ensures consistent audio calls across the entire project.
/// </summary>
public static class SoundUtils
{
    /// <summary>
    /// Plays a UI/2D sound (spatialBlend should be 0 in SoundData).
    /// </summary>
    public static void Play2D(SoundData soundData)
    {
        if (soundData == null || SoundManager.Instance == null)
            return;

        SoundManager.Instance
            .CreateSoundBuilder()
            .Play(soundData);
    }

    /// <summary>
    /// Plays a sound at a specific world position (3D sound).
    /// </summary>
    public static void PlayAtPosition(SoundData soundData, Vector3 position)
    {
        if (soundData == null || SoundManager.Instance == null)
            return;

        SoundManager.Instance
            .CreateSoundBuilder()
            .WithPosition(position)
            .Play(soundData);
    }

    /// <summary>
    /// Plays a sound attached to a transform (for moving objects).
    /// </summary>
    public static void PlayAttached(SoundData soundData, Transform parent)
    {
        if (soundData == null || SoundManager.Instance == null || parent == null)
            return;

        SoundManager.Instance
            .CreateSoundBuilder()
            .WithParent(parent)
            .WithPosition(parent.position)
            .Play(soundData);
    }
}
