using System;
using UnityEngine;
using UnityEngine.Audio;

[Serializable]
public class SoundData
{
    public AudioClip clip;
    public AudioMixerGroup mixerGroup;
    public bool loop;
    public bool playOnAwake;
    public bool frequentSound;

    public bool mute;
    public bool bypassEffects;
    public bool bypassListenerEffects;
    public bool bypassReverbZones;

    [field: Range(0, 256)]
    public int priority = 128;

    [field: Range(0, 1f)]
    public float volume = 1f;

    [field: Range(-3f, 3f)]
    public float pitch = 1f;

    [field: Range(-1f, 1f)]
    public float panStereo;

    [field: Range(0,1f)]
    public float spatialBlend = 0f;

    [field: Range(0f, 1.1f)]
    public float reverbZoneMix = 1f;

    [field: Range(0f, 5f)]
    public float dopplerLevel = 1f;

    [field: Range(0f, 360f)]
    public float spread;

    public float minDistance = 1f;
    public float maxDistance = 500f;

    public bool ignoreListenerVolume;
    public bool ignoreListenerPause;

    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
}
