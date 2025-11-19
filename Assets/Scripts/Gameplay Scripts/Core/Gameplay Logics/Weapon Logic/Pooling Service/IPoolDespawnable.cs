
/// <summary>
/// Marker interface for pooled gameplay objects (e.g., projectiles)
/// that can be safely returned to their pool on demand (e.g., on border hit).
/// </summary>
public interface IPoolDespawnable
{
    /// <summary>Return this instance to its originating pool immediately.</summary>
    void DespawnToPool();
}
