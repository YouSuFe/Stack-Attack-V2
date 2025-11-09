using System.Collections;
using UnityEngine;

public static class PauseAwareCoroutine
{
    /// <summary>
    /// Waits for 'seconds' of gameplay time, but fully halts while paused.
    /// Uses scaled deltaTime by default so it also halts if you set timeScale=0.
    /// </summary>
    public static IEnumerator Delay(float seconds)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            // If globally paused, hang until resumed.
            while (PauseManager.Instance != null && PauseManager.Instance.IsGameplayStopped)
                yield return null;

            remaining -= Time.deltaTime; // if timeScale=0, this is 0 and we hang; if not, we still hang due to the loop above
            yield return null;
        }
    }

    /// <summary>
    /// Wait until (baseTime + offsetSeconds) in gameplay time, pausing cleanly during global pause.
    /// </summary>
    public static IEnumerator Until(float baseTime, float offsetSeconds)
    {
        float remaining = Mathf.Max(0f, (baseTime + Mathf.Max(0f, offsetSeconds)) - Time.time);
        while (remaining > 0f)
        {
            while (PauseManager.Instance != null && PauseManager.Instance.IsGameplayStopped)
                yield return null;

            remaining -= Time.deltaTime;
            yield return null;
        }
    }
}

