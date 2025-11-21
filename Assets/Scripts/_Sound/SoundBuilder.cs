using UnityEngine;

public class SoundBuilder
{
    readonly SoundManager soundManager;
    Vector3 position = Vector3.zero;
    bool randomPitch;
    float loopDuration = -1f; // -1 means no loop duration limit
    Transform parentTransform; // New field for setting a parent transform

    public SoundBuilder(SoundManager soundManager)
    {
        this.soundManager = soundManager;
    }

    public SoundBuilder WithPosition(Vector3 position)
    {
        this.position = position;
        return this;
    }

    public SoundBuilder WithRandomPitch()
    {
        this.randomPitch = true;
        return this;
    }

    public SoundBuilder WithLoopDuration(float duration)
    {
        loopDuration = duration;
        return this;
    }

    public SoundBuilder WithParent(Transform parent)
    {
        parentTransform = parent;
        return this;
    }

    public SoundEmitter Play(SoundData soundData)
    {
        if (soundData == null)
        {
            Debug.LogError("SoundData is null");
            return null;
        }

        if (!soundManager.CanPlaySound(soundData)) return null;

        SoundEmitter soundEmitter = soundManager.Get();
        soundEmitter.Initialize(soundData);

        // Set position and parent if specified
        soundEmitter.transform.position = position;
        soundEmitter.transform.parent = parentTransform != null ? parentTransform : soundManager.transform;


        if (randomPitch)
        {
            soundEmitter.WithRandomPitch();
        }

        if (soundData.frequentSound)
        {
            soundEmitter.Node = soundManager.FrequentSoundEmitters.AddLast(soundEmitter);
        }

        soundEmitter.Play(loopDuration > 0 ? loopDuration : -1f);

        return soundEmitter;
    }
}

