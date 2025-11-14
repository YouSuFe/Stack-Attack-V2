using TMPro;
using UnityEngine;
/// <summary>
/// Displays the enemy's health as text above the enemy.
/// Subscribes to EnemyHealth and updates a world-space TextMeshPro UI.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealthText : MonoBehaviour
{
    #region Serialized
    [Header("References")]
    [SerializeField, Tooltip("EnemyHealth component to observe. If null, will try to auto-find on this GameObject or its parents.")]
    private EnemyHealth enemyHealth;

    [SerializeField, Tooltip("TextMeshProUGUI used to display the health value.")]
    private TMP_Text healthText;

    [Header("Display")]
    [SerializeField, Tooltip("If true, show as `current / max`. If false, only show current.")]
    private bool showMaxHealth = true;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Auto-wire components if not set in Inspector
        if (enemyHealth == null)
        {
            // Try to find on self or parents (enemy root often has EnemyHealth).
            if (!TryGetComponent(out enemyHealth))
            {
                enemyHealth = GetComponentInParent<EnemyHealth>();
            }
        }

        if (healthText == null)
        {
            healthText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged += HandleHealthChanged;

            // Initial refresh in case health is already initialized
            HandleHealthChanged(enemyHealth.CurrentHealth, enemyHealth.MaxHealth);
        }
        else
        {
            Debug.LogWarning($"[{nameof(EnemyHealthText)}] No EnemyHealth found on {gameObject.name}. Health text will not update.");
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }
    #endregion

    #region Private Methods
    private void HandleHealthChanged(int current, int max)
    {
        if (healthText == null)
            return;

        if (showMaxHealth)
        {
            healthText.text = $"{current}/{max}";
        }
        else
        {
            healthText.text = current.ToString();
        }
    }
    #endregion
}

