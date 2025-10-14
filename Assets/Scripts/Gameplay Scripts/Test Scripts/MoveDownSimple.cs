using UnityEngine;

public class MoveDownSimple : MonoBehaviour
{
    [SerializeField] private float speed = 3f; // units per second

    private void Update()
    {
        // Frame-rate independent: moves straight down in world space
        transform.Translate(Vector3.down * speed * Time.deltaTime, Space.World);
    }
}
