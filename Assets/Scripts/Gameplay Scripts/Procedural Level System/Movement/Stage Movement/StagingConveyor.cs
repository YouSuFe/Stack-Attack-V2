using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Single manager that moves all staged objects downwards at a unified speed.
/// No parenting is required; agents register/unregister themselves.
/// </summary>
public class StagingConveyor : MonoBehaviour
{
    #region Singleton
    public static StagingConveyor Instance { get; private set; }
    #endregion

    #region Serialized
    [SerializeField, Tooltip("Unified downward speed for all off-screen staged objects (units/sec).")]
    private float conveyorSpeed = 5f;
    #endregion

    #region Private Fields
    private readonly List<SpawnStageAgent> agents = new List<SpawnStageAgent>();
    private readonly List<SpawnStageAgent> toRemove = new List<SpawnStageAgent>();
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[StagingConveyor] Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        float delta = Time.deltaTime * conveyorSpeed;
        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            if (agent == null)
            {
                toRemove.Add(agent);
                continue;
            }

            if (agent.State == SpawnStageAgent.StageState.Staging)
            {
                // Unified downward movement while off-screen
                Vector3 p = agent.transform.position;
                p.y -= delta;
                agent.transform.position = p;
            }
        }

        if (toRemove.Count > 0)
        {
            foreach (var a in toRemove) { agents.Remove(a); }
            toRemove.Clear();
        }
    }
    #endregion

    #region Public API
    public void Register(SpawnStageAgent agent)
    {
        if (!agents.Contains(agent))
            agents.Add(agent);
    }

    public void Unregister(SpawnStageAgent agent)
    {
        agents.Remove(agent);
    }

    public void SetSpeed(float speed)
    {
        conveyorSpeed = Mathf.Max(0f, speed);
    }

    public float GetSpeed() => conveyorSpeed;
    #endregion
}
