using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

// Agent script for a cooperative push block task using Unity ML-Agents
public class PushAgentCollab : Agent
{
    // Reference to the shared environment settings
    private PushBlockSettings m_PushBlockSettings;

    // Reference to the agent's Rigidbody for physics-based movement
    private Rigidbody m_AgentRb;

    // Called when the agent is first created (before Initialize)
    protected override void Awake()
    {
        base.Awake();
        // Locate the PushBlockSettings object in the scene
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();
    }

    // Called once at the start or when the agent is reset
    public override void Initialize()
    {
        // Get the Rigidbody component attached to the agent
        m_AgentRb = GetComponent<Rigidbody>();
    }

    // Translates discrete action inputs into movement and rotation
    public void MoveAgent(ActionSegment<int> act)
    {
        var dirToGo = Vector3.zero;   // Movement direction
        var rotateDir = Vector3.zero; // Rotation direction

        // Decode discrete action to movement or rotation
        switch (act[0])
        {
            case 1: dirToGo = transform.forward; break;          // Move forward
            case 2: dirToGo = -transform.forward; break;         // Move backward
            case 3: rotateDir = transform.up; break;             // Rotate clockwise
            case 4: rotateDir = -transform.up; break;            // Rotate counter-clockwise
            case 5: dirToGo = -transform.right * 0.75f; break;   // Strafe left
            case 6: dirToGo = transform.right * 0.75f; break;    // Strafe right
        }

        // Apply rotation to the agent
        transform.Rotate(rotateDir, Time.fixedDeltaTime * 200f);

        // Apply movement force based on environment-defined speed
        m_AgentRb.AddForce(dirToGo * m_PushBlockSettings.agentRunSpeed, ForceMode.VelocityChange);
    }

    // Called every simulation step to apply actions from the model or heuristic
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Move the agent based on discrete action input
        MoveAgent(actionBuffers.DiscreteActions);
    }
}