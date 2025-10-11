using UnityEngine;

public class LinearProjectile : ProjectileBase
{
    [Header("Motion")]
    [SerializeField] private float speed = 12f;   // units/sec along +Y (local)

    protected override void TickMotion(float dt)
    {
        transform.position += transform.up * speed * dt;
    }
}

