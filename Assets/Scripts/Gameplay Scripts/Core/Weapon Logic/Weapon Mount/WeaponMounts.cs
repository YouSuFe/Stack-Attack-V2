using UnityEngine;
/// <summary>
/// Holds all scene-specific spawn/muzzle references and per-rig tuning values
/// for the player's weapons. Assign these once in the inspector on the Player.
/// </summary>
public class WeaponMounts : MonoBehaviour
{
    [Header("Basic (StraightLine)")]
    [SerializeField] private Transform basicFireOrigin;
    [SerializeField] private float basicHorizontalSpacing = 0.7f;
    [SerializeField] private float basicRowVerticalOffset = 0.15f;

    [Header("Missile (AlternatingBurst)")]
    [SerializeField] private Transform missileFireOrigin;
    [SerializeField] private Transform missileLeftMuzzle;
    [SerializeField] private Transform missileRightMuzzle;
    [SerializeField] private float missileFallbackSideOffsetX = 0.6f;

    [Header("Kunai (FanSequential)")]
    [SerializeField] private Transform kunaiFireOrigin;
    [SerializeField] private float kunaiFanStepDegrees = 5f;

    // Read-only accessors (useful for debugging or future logic)
    public Transform BasicFireOrigin => basicFireOrigin;
    public float BasicHorizontalSpacing => basicHorizontalSpacing;
    public float BasicRowVerticalOffset => basicRowVerticalOffset;

    public Transform MissileFireOrigin => missileFireOrigin;
    public Transform MissileLeftMuzzle => missileLeftMuzzle;
    public Transform MissileRightMuzzle => missileRightMuzzle;
    public float MissileFallbackSideOffsetX => missileFallbackSideOffsetX;

    public Transform KunaiFireOrigin => kunaiFireOrigin;
    public float KunaiFanStepDegrees => kunaiFanStepDegrees;
}

