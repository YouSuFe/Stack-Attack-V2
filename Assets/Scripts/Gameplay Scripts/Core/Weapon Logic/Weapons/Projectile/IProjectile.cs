using UnityEngine;

public interface IProjectile
{
    void Initialize(GameObject owner, int damageAmount, int piercing);
}
