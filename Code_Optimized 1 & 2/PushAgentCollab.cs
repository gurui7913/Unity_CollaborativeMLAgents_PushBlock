using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using System.Collections;
using System.Collections.Generic;

public class PushAgentCollab : Agent
{
    private Rigidbody m_AgentRb;
    private PushBlockEnvController m_EnvController;
    private PushBlockSettings m_PushBlockSettings;

    // Idle detection
    private float idleTime = 0f;
    private float idleThreshold = 2.0f;
    //private float extraIdlePenaltyPerStep = -0.05f;
    private float minEffectiveSpeed = 0.1f;

    // Debug helper
    public bool enableDebugLogs = true;
    private float lastPushTime = 0f;
    private float debugLogInterval = 1.0f;

    // Contribution tracking for continuous contact
    private float contactCooldown = 0.1f;  // Shorter cooldown to record contributions more frequently
    private Dictionary<int, float> lastContactTime = new Dictionary<int, float>();

    public override void Initialize()
    {
        m_AgentRb = GetComponent<Rigidbody>();
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();
        // Get the reference to the EnvController in the scene
        m_EnvController = FindObjectOfType<PushBlockEnvController>();
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Agent movement
        MoveAgent(actionBuffers.DiscreteActions);

        // Example of idle penalty
        float velocityMagnitude = m_AgentRb.velocity.magnitude;
        if (velocityMagnitude < minEffectiveSpeed)
        {
            idleTime += Time.fixedDeltaTime;
            if (idleTime >= idleThreshold)
            {
                //AddReward(extraIdlePenaltyPerStep);
            }
        }
        else
        {
            idleTime = 0f;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;
        if (Input.GetKey(KeyCode.W)) discreteActions[0] = 1;
        else if (Input.GetKey(KeyCode.S)) discreteActions[0] = 2;
        else if (Input.GetKey(KeyCode.D)) discreteActions[0] = 3;
        else if (Input.GetKey(KeyCode.A)) discreteActions[0] = 4;
    }

    private void MoveAgent(ActionSegment<int> act)
    {
        Vector3 dirToGo = Vector3.zero;
        Vector3 rotateDir = Vector3.zero;
        int action = act[0];

        switch (action)
        {
            case 1: dirToGo = transform.forward; break;
            case 2: dirToGo = -transform.forward; break;
            case 3: rotateDir = transform.up; break;
            case 4: rotateDir = -transform.up; break;
        }
        transform.Rotate(rotateDir, Time.fixedDeltaTime * 200f);
        m_AgentRb.AddForce(dirToGo * m_PushBlockSettings.pushForce, ForceMode.VelocityChange); 
    }

    // Improved collision detection - when a collision begins
    private void OnCollisionEnter(Collision collision)
    {
        RecordCollision(collision, 1.2f);  // Increased weight for initial contact
    }

    // Continuous collision detection - when collision stays
    private void OnCollisionStay(Collision collision)
    {
        RecordCollision(collision, 0.4f);  // Reduced weight for sustained contact
    }

    // Unified collision handling
    private void RecordCollision(Collision collision, float weightMultiplier)
    {
        // Check if collided with a block
        var blockId = collision.gameObject.GetComponent<BlockTypeIdentifier>();
        if (blockId != null)
        {
            int blockInstanceId = collision.gameObject.GetInstanceID();

            // Check cooldown (shortened to record more frequent contributions)
            float currentTime = Time.time;
            if (!lastContactTime.ContainsKey(blockInstanceId) || 
                currentTime - lastContactTime[blockInstanceId] >= contactCooldown)
            {
                // Calculate collision force based on relative velocity and angle
                float relativeSpeed = collision.relativeVelocity.magnitude;
                float baseImpactForce = Mathf.Max(0.5f, relativeSpeed * 0.6f);
                float impactForce = baseImpactForce * weightMultiplier;

                // Ensure even slow collisions are recorded
                if (impactForce < 0.3f)
                    impactForce = 0.3f;

                // Get the contribution tracker from the block
                var contributionTracker = collision.gameObject.GetComponent<BlockContributionTracker>();
                if (contributionTracker != null)
                {
                    // Add this agent's contribution
                    contributionTracker.AddAgentContribution(GetInstanceID(), impactForce);

                    // Update last contact time
                    lastContactTime[blockInstanceId] = currentTime;

                    // Debug logs
                    if (enableDebugLogs && (currentTime - lastPushTime > debugLogInterval || impactForce > 1.0f))
                    {
                        Debug.Log($"Agent {gameObject.name} (ID:{GetInstanceID()}) pushed block {blockId.blockType}, force: {impactForce:F2}");
                        lastPushTime = currentTime;
                    }
                }
            }
        }
        // Check if collided with another agent
        else if (collision.gameObject.GetComponent<PushAgentCollab>() != null)
        {
            // When colliding with another agent, propagate force to nearby blocks
            PropagateForceToNearbyBlocks(transform.position, m_PushBlockSettings.pushForce * 0.6f);
        }
    }

    // Improved force propagation to nearby blocks
    private void PropagateForceToNearbyBlocks(Vector3 position, float forceMagnitude)
    {
        // Detect nearby blocks (increased radius)
        Collider[] hitColliders = Physics.OverlapSphere(position, 2.5f);
        foreach (var hitCollider in hitColliders)
        {
            var blockId = hitCollider.GetComponent<BlockTypeIdentifier>();
            if (blockId != null)
            {
                // Calculate distance factor
                float distance = Vector3.Distance(position, hitCollider.transform.position);
                float distanceFactor = Mathf.Max(0, 1 - (distance / 2.5f));

                // Compute transferred force based on distance
                float transferredForce = forceMagnitude * distanceFactor * 0.8f;

                // Add contribution to the block (lower minimum threshold)
                var contributionTracker = hitCollider.GetComponent<BlockContributionTracker>();
                if (contributionTracker != null && transferredForce > 0.01f)
                {
                    contributionTracker.AddAgentContribution(GetInstanceID(), transferredForce);

                    if (enableDebugLogs && transferredForce > 0.2f)
                    {
                        Debug.Log($"Agent {gameObject.name} indirectly affected block {blockId.blockType}, distance: {distance:F2}, transferred force: {transferredForce:F2}");
                    }
                }
            }
        }
    }

    // Trigger event handler when touching the goal
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            var blockId = GetComponent<BlockTypeIdentifier>();
            if (blockId != null && m_EnvController != null)
            {
                m_EnvController.OnBlockGoalScored(blockId.blockType);
            }
        }
    }

    // Override AgentReset to ensure all states are reset
    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();
        // Reset contact times and idle time
        lastContactTime.Clear();
        idleTime = 0f;
        lastPushTime = 0f;
    }
}
