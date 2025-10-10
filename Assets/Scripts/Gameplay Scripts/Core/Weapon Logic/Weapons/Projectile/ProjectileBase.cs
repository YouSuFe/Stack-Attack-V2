using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public abstract class ProjectileBase : MonoBehaviour, IDamageDealer, IProjectile
{
    [Header("Damage")]
    [SerializeField] private int damageAmount = 1;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetimeSeconds = 6f;

    [Header("Piercing")]
    [SerializeField] private int startingPiercing = 0;

    private float lifeTimer;
    private int remainingPiercing;
    private GameObject owner;

    // ---- IDamageDealer ----
    public int DamageAmount => damageAmount;
    public GameObject Owner => owner != null ? owner : gameObject;

    // ---- IProjectile ----
    public void Initialize(GameObject initOwner, int initDamage, int initPiercing)
    {
        owner = initOwner;
        damageAmount = Mathf.Max(1, initDamage);
        startingPiercing = Mathf.Max(0, initPiercing);
        remainingPiercing = startingPiercing;
    }

    protected virtual void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        remainingPiercing = startingPiercing;
    }

    protected virtual void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        TickMotion(Time.deltaTime);
    }

    protected abstract void TickMotion(float dt);

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Don't hit our owner
        if (owner != null && other.gameObject == owner)
            return;

        if (other.TryGetComponent<IDamageable>(out var target))
        {
            target.TakeDamage(Mathf.Max(1, damageAmount), Owner);

            if (remainingPiercing > 0)
            {
                remainingPiercing--;
                // Still alive: keep flying (do not destroy)
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}


