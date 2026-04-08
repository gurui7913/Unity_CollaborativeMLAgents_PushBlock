using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class BlockContributionTracker : MonoBehaviour
{
    // Stores the contribution value of each agent
    private Dictionary<int, float> agentContributions = new Dictionary<int, float>();
    // Stores the last time each agent contributed
    private Dictionary<int, float> lastContributionTime = new Dictionary<int, float>();
    // Set of currently active agents
    public HashSet<int> contributingAgents = new HashSet<int>();

    [Header("Contribution Settings")]
    // Contributions below this threshold are considered no longer effective
    public float directContactThreshold = 0.0005f; // Lower threshold to make contributions easier to track
    // Decay multiplier applied to contribution values each frame (smaller = faster decay)
    public float indirectContactDecay = 0.98f; // Slower decay rate for more persistent contributions
    // Agents with contributions within this time window are considered "active"
    public float activeTimeWindow = 5.0f; // Extend active time window

    [Header("Debug Settings")]
    public bool enableDebugLogs = true;
    private bool goalApproaching = false;
    private float lastLogTime = 0f;
    private float logInterval = 1.0f;

    // Add contribution from an agent
    public void AddAgentContribution(int agentId, float contributionValue)
    {
        if (!agentContributions.ContainsKey(agentId))
        {
            agentContributions[agentId] = 0f;
            lastContributionTime[agentId] = Time.time;
        }

        // Accumulate contribution
        agentContributions[agentId] += contributionValue;
        // Update last contribution time
        lastContributionTime[agentId] = Time.time;
        // Add to active agent set
        contributingAgents.Add(agentId);

        // If the block is moving fast and near the goal, start logging
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && rb.velocity.magnitude > 0.2f) // Lower speed threshold
        {
            goalApproaching = true;
        }
    }
    
    // Get the number of active collaborating agents
    public int GetActiveAgentCount()
    {
        float currentTime = Time.time;
        int count = 0;
        List<int> activeAgentIds = new List<int>();

        foreach (var agentId in contributingAgents)
        {
            // Check if agent contributed within the active time window
            if (currentTime - lastContributionTime[agentId] < activeTimeWindow)
            {
                count++;
                activeAgentIds.Add(agentId);
            }
        }

        if (enableDebugLogs && goalApproaching)
        {
            //Debug.Log($"Block {gameObject.name} active agents: {count}, IDs: {string.Join(",", activeAgentIds)}");
        }

        return count;
    }

    // Get the total effective force from active agents
    public float GetTotalEffectiveForce()
    {
        float totalForce = 0f;
        float currentTime = Time.time;

        foreach (var entry in agentContributions)
        {
            // Only include contributions within the active window
            if (currentTime - lastContributionTime[entry.Key] < activeTimeWindow)
            {
                totalForce += entry.Value;
            }
        }

        return totalForce;
    }

    // Get all agent contributions (for debugging)
    public Dictionary<int, float> GetAllContributions()
    {
        return new Dictionary<int, float>(agentContributions);
    }

    // Decay contribution values every frame; remove if below threshold
    void FixedUpdate()
    {
        // Log block status
        if (enableDebugLogs && goalApproaching && Time.time - lastLogTime > logInterval)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                string contributionsLog = "";
                foreach (var entry in agentContributions)
                {
                    contributionsLog += $"Agent {entry.Key}: {entry.Value:F2} ({Time.time - lastContributionTime[entry.Key]:F1}s ago), ";
                }

                //Debug.Log($"Block {gameObject.name} speed: {rb.velocity.magnitude:F2}, contributions: {contributionsLog}");
                lastLogTime = Time.time;
            }
        }

        // Apply decay
        foreach (var agentId in agentContributions.Keys.ToList())
        {
            agentContributions[agentId] *= indirectContactDecay;

            // Remove entry from dictionary if below threshold but keep in active set
            if (agentContributions[agentId] < directContactThreshold)
            {
                agentContributions.Remove(agentId);
            }
        }
    }

    // Reset goal-approaching state
    public void SetApproachingGoal(bool approaching)
    {
        goalApproaching = approaching;
    }
    
    // Clear all contribution data
    public void ResetContributions()
    {
        agentContributions.Clear();
        contributingAgents.Clear();
        lastContributionTime.Clear();
        goalApproaching = false;
    }

    // Log current state when approaching the goal
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            SetApproachingGoal(true);
            if (enableDebugLogs)
            {
                //Debug.Log($"Block {gameObject.name} is near goal! Active agents: {GetActiveAgentCount()}, Total force: {GetTotalEffectiveForce():F2}");

                // Print agent contribution details
                string agentLog = "";
                foreach (var agentId in contributingAgents)
                {
                    float timeSinceLastContribution = Time.time - lastContributionTime[agentId];
                    if (timeSinceLastContribution < activeTimeWindow) 
                    {
                        float contributionValue = agentContributions.ContainsKey(agentId) ? 
                            agentContributions[agentId] : 0f;
                        agentLog += $"Agent {agentId}: {contributionValue:F2} (contributed {timeSinceLastContribution:F1}s ago), ";
                    }
                }
                //Debug.Log($"Agent contribution details: {agentLog}");
            }
        }
    }
}
