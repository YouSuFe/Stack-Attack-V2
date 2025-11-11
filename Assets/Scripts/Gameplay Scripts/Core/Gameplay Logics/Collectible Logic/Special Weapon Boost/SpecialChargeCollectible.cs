// CollectibleBase.cs
using UnityEngine;

public class SpecialChargeCollectible : CollectibleBase, IInitializableFromContext
{
    private SpecialSkillDriver specialSkillDriver;

    public void Initialize(SpawnInitContext context)
    {
        if (!specialSkillDriver) specialSkillDriver = context.SpecialSkillDriver;
    }

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
