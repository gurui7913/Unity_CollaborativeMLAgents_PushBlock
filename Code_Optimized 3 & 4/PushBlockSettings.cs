using UnityEngine;

// Scriptable settings container for the Push Block environment
public class PushBlockSettings : MonoBehaviour
{
    // Speed at which the agent moves (applied as force in the environment)
    public float agentRunSpeed;

    // Speed at which the agent rotates (typically affects turn responsiveness)
    public float agentRotationSpeed;

    // Multiplier to reduce the spawn area near the edges (prevents spawning too close to boundaries)
    public float spawnAreaMarginMultiplier;

    // Material used to visually indicate that a goal has been successfully scored
    public Material goalScoredMaterial;

    // Material used to indicate a failure or undesired outcome (e.g., agent collides with wall)
    public Material failMaterial;
}