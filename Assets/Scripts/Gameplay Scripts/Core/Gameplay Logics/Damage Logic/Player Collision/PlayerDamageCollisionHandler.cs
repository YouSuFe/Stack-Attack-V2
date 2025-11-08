using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerDamageCollisionHandler : MonoBehaviour
{
    [Header("Kill On Touch")]
    [SerializeField] private bool killOthersOnTriggerEnter = true;
    [SerializeField] private int lethalDamageAmount = 999999;

    private PlayerHealth playerHealth;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !playerHealth.IsAlive || other.CompareTag("Projectile")) return;

        // 1) Apply incoming damage
        if (other.TryGetComponent<IDamageDealer>(out var dealer))
        {
            int amount = Mathf.Max(1, dealer.DamageAmount);
            GameObject source = dealer.Owner != null ? dealer.Owner : other.gameObject;

            // Prevent self-damage:
            if (source == gameObject)
                return;

            playerHealth.TakeDamage(amount, source);
        }

        // 2) Kill anything damageable that touches the player
        if (killOthersOnTriggerEnter && other.TryGetComponent<IDamageable>(out var damageable))
        {
            // Avoid nuking ourselves if overlapping colliders exist
            if (!ReferenceEquals(damageable, (IDamageable)playerHealth))
            {
                damageable.TakeDamage(lethalDamageAmount, gameObject);
            }
        }
    }
}

