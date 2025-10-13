using UnityEngine;

public class PowerupUITestSpawner : MonoBehaviour
{
    [SerializeField] private PowerupPanelUIController powerupPanelUIController;
    [SerializeField] private float secondsBetweenRolls = 5f;

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= secondsBetweenRolls)
        {
            timer = 0f;
            if (powerupPanelUIController != null && !powerupPanelUIController.gameObject.activeSelf)
                powerupPanelUIController.ShowAndRoll();
        }
    }
}

