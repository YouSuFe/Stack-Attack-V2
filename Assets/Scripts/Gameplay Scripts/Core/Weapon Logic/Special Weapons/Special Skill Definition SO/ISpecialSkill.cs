using UnityEngine;

public interface ISpecialSkill
{
    void Initialize(SpecialSkillDefinitionSO definition, GameObject owner);
    bool TryActivate();                // return true when the skill successfully starts
    void TickActive(float deltaTime);  // called each frame while active
    void Stop();                       // force stop (on end/disable)
}
