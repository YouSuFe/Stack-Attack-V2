using UnityEngine;

[DisallowMultipleComponent]
public class PinataDamageReceiver : MonoBehaviour
{
    #region Private
    private PinataMeter meter;
    #endregion

    #region Public API
    public void BindPinataMeter(PinataMeter m)
    {
        meter = m;
        gameObject.SetActive(meter != null);
    }

    public void ApplyHit(int damage)
    {
        meter?.ApplyHit(damage);
    }
    #endregion

    // Optional trigger hook if you use physics projectiles:
    /*
    private void OnTriggerEnter2D(Collider2D other)
    {
        var proj = other.GetComponent<YourProjectileDamage>();
        if (proj)
        {
            ApplyHit(proj.Damage);
            Destroy(other.gameObject);
        }
    }
    */
}
