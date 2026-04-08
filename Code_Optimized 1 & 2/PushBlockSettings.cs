using UnityEngine;

public class PushBlockSettings : MonoBehaviour
{
    /// <summary>
    /// The "walking speed" of the agents in the scene.
    /// </summary>
    public float agentRunSpeed;
    public float pushForce = 0.1f;

    /// <summary>
    /// The agent rotation speed.
    /// Every agent will use this setting.
    /// </summary>
    public float agentRotationSpeed;

    /// <summary>
    /// The spawn area margin multiplier.
    /// ex: .9 means 90% of spawn area will be used.
    /// .1 margin will be left (so players don't spawn off of the edge).
    /// The higher this value, the longer training time required.
    /// </summary>
    public float spawnAreaMarginMultiplier;

    /// <summary>
    /// These materials are used for visual feedback.
    /// Priority is given to feedback related to collaboration.
    /// Displayed when multiple agents successfully complete a task together.
    /// </summary>
    public Material collaborationScoreMaterial;
    
    /// <summary>
    /// Displayed when collaboration fails (e.g., insufficient combined force or incorrect agent configuration).
    /// </summary>
    public Material failedCollaborationMaterial;
    
    /// <summary>
    /// When a goal is scored the ground will switch to this
    /// material for a few seconds.
    /// </summary>
    public Material goalScoredMaterial;

    /// <summary>
    /// When an agent fails, the ground will turn this material for a few seconds.
    /// </summary>
    public Material failMaterial;
}
