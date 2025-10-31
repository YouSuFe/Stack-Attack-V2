using UnityEngine;
/// <summary>
/// Attach to every spawned object so the sequencer can track when it goes away.
/// Works with normal Destroy(). For pooling, call Despawn() instead of Destroy().
/// </summary>
[DisallowMultipleComponent]
public class SegmentObject : MonoBehaviour
{
    #region Private Fields
    private LevelSegmentSequencer owner;
    private int segmentIndex;
    private bool bound;
    private bool despawned; // prevents double notify (e.g., pooled disable + later destroy)
    #endregion

    #region Public API
    /// <summary>Bind this object to a sequencer/segment so it counts toward completion.</summary>
    public void Bind(LevelSegmentSequencer sequencer, int owningSegmentIndex)
    {
        owner = sequencer;
        segmentIndex = owningSegmentIndex;
        bound = (owner != null);
        despawned = false;
    }

    /// <summary>
    /// For pooling: call this when returning the object to pool instead of Destroy().
    /// </summary>
    public void Despawn()
    {
        if (despawned) return;
        despawned = true;
        if (bound && owner != null)
            owner.NotifyObjectDestroyed(segmentIndex);
    }
    #endregion

    #region Unity
    private void OnDestroy()
    {
        if (despawned) return; // already counted
        if (bound && owner != null)
        {
            owner.NotifyObjectDestroyed(segmentIndex);
        }
    }
    #endregion
}
