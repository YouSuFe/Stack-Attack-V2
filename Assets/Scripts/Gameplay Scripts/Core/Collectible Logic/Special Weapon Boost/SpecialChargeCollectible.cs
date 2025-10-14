// CollectibleBase.cs
using UnityEngine;

public class SpecialChargeCollectible : CollectibleBase
{
    [Header("Discovery")]
    [Tooltip("If null, we will search on the Player.")]
    [SerializeField] private SpecialSkillDriver specialSkillDriver;

    protected override bool OnCollected(GameObject player)
    {
        // Resolve driver if not assigned
        if (specialSkillDriver == null)
            specialSkillDriver = player.GetComponent<SpecialSkillDriver>();

        if (specialSkillDriver == null)
        {
            Debug.LogWarning("SpecialChargeCollectible: Could not find SpecialSkillDriver on player.");
            return false;
        }

        // Fill to max so the player can fire on release (when in combat)
        specialSkillDriver.FillChargeToMax();  // method we added
        return true;
    }
}
