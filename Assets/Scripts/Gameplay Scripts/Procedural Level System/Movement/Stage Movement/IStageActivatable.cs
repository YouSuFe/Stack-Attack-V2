using UnityEngine;

/// <summary>
/// Minimal lifecycle contract so movers can be paused off-screen,
/// capture their baselines exactly at the entry gate, and then resume.
/// </summary>
public interface IStageActivatable
{
    /// <summary>Stop applying movement logic (no Update work).</summary>
    void PauseMover();

    /// <summary>
    /// Capture baselines (e.g., startX, radius/phase) using the position at the entry gate.
    /// Called right before ResumeMover().
    /// </summary>
    void ArmAtEntry(Vector3 entryWorldPos);

    /// <summary>Start applying movement logic.</summary>
    void ResumeMover();
}
