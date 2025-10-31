using UnityEngine;
/// <summary>
/// Destroys any object that enters this trigger.
/// This ensures objects that fall below the screen are cleaned up properly.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class KillZoneTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object has a SegmentObject (optional but recommended)
        var segObj = other.GetComponent<SegmentObject>();
        if (segObj != null)
        {
            // Destroy the object, will automatically notify the sequencer
            Destroy(segObj.gameObject);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Destroy(collision.gameObject);
    }
}

