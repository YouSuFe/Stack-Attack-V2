using UnityEngine;

/// <summary>
/// Very basic test enemy for validating damage, invulnerability, and instant-kill.
/// Moves toward the player each frame and damages them on contact.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TestEnemy : MonoBehaviour, IDamageable, IDamageDealer
{
    [Header("Stats")]
    [SerializeField] private int health = 3;
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Visuals")]
    [SerializeField] private GameObject deathEffect; // optional prefab (particle, sprite flash, etc.)

    private Transform playerTarget;
    private Collider2D enemyCollider2D;

    public bool IsAlive => health > 0;
    public int DamageAmount => contactDamage;
    public GameObject Owner => gameObject;

    private void Awake()
    {
        enemyCollider2D = GetComponent<Collider2D>();
        enemyCollider2D.isTrigger = true; // so we use trigger logic (same as player)
    }

    private void Start()
    {
        // Find the player by tag if not manually assigned
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTarget = player.transform;
    }

    private void Update()
    {
        if (!IsAlive || playerTarget == null) return;

        // Simple chase toward player
        Vector3 current = transform.position;
        Vector3 target = playerTarget.position;
        Vector3 direction = (target - current).normalized;

        transform.position += direction * moveSpeed * Time.deltaTime;
    }

    public void TakeDamage(int damageAmount, GameObject damageSource)
    {
        if (!IsAlive) return;

        health -= Mathf.Max(1, damageAmount);

        if (health <= 0)
        {
            Die(damageSource);
        }
    }

    private void Die(GameObject killer)
    {
        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        Debug.Log($"{this.name} is killed by {killer.name}");

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"{gameObject.name} is collided with {other.name}");
    }
}
