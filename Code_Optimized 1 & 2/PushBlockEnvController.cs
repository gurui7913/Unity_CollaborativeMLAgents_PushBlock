using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgentsExamples;

public class PushBlockEnvController : MonoBehaviour
{
    [Header("Agent List")]
    public List<PushAgentCollab> AgentsList;

    [Header("Block List")]
    public List<Transform> BlocksList;

    [Header("Max Steps")]
    public int MaxEnvironmentSteps = 5000;

    [Header("Debug Settings")]
    public bool enableDebugLogs = true;

    private SimpleMultiAgentGroup m_AgentGroup;
    private Dictionary<int, float> successMemory = new Dictionary<int, float>();

    // Used to track the remaining number of blocks. The episode ends when all blocks are pushed into the goal.
    private int m_RemainingBlocks;
    private int m_ResetTimer;

    // Ground and scene related
    public GameObject ground;
    public GameObject area;
    private Renderer m_GroundRenderer;
    private Material m_GroundMaterial;
    private Bounds areaBounds;
    private PushBlockSettings m_PushBlockSettings;

    void Start()
    {
        // 1. Create a single multi-agent group
        m_AgentGroup = new SimpleMultiAgentGroup();

        // 2. Register all agents in the scene to the same group
        foreach (var agent in AgentsList)
        {
            m_AgentGroup.RegisterAgent(agent);
        }

        // Basic initialization
        m_PushBlockSettings = FindObjectOfType<PushBlockSettings>();
        m_GroundRenderer = ground.GetComponent<Renderer>();
        m_GroundMaterial = m_GroundRenderer.material;
        areaBounds = ground.GetComponent<Collider>().bounds;

        ResetScene();
    }

    void FixedUpdate()
    {
        m_ResetTimer++;
        // If timeout occurs before task is finished, force end
        if (m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            m_AgentGroup.GroupEpisodeInterrupted();
            ResetScene();
        }

        // Give a small negative reward over time to encourage faster completion
        m_AgentGroup.AddGroupReward(-0.05f / MaxEnvironmentSteps);
    }

    /// <summary>
    /// Called by collision detection script or agent when a block is pushed into the goal.
    /// Implements logic for different block types: Block3 requires 3 agents, Block2 requires 2, etc.
    /// </summary>
    public void OnBlockGoalScored(BlockTypeIdentifier.BlockType blockType)
    {
        // Find the corresponding block of the specified type
        GameObject targetBlock = null;
        foreach (var b in BlocksList)
        {
            if (b.GetComponent<BlockTypeIdentifier>().blockType == blockType)
            {
                targetBlock = b.gameObject;
                break;
            }
        }

        if (targetBlock == null) return;

        // Get the contribution tracker for the block
        var contributionTracker = targetBlock.GetComponent<BlockContributionTracker>();
        if (contributionTracker == null) return;

        // Determine required agents and reward value by block type
        int requiredAgents = 0;
        float rewardValue = 0f;
        string blockTypeName = "";

        switch (blockType)
        {
            case BlockTypeIdentifier.BlockType.Block3:
                requiredAgents = 3;
                rewardValue = 50f;
                blockTypeName = "Block3";
                break;
            case BlockTypeIdentifier.BlockType.Block2:
                requiredAgents = 2;
                rewardValue = 30f;
                blockTypeName = "Block2";
                break;
            default: // Block1
                requiredAgents = 1;
                rewardValue = 20f;
                blockTypeName = "Block1";
                break;
        }

        // Get number of collaborating agents
        int activeAgents = contributionTracker.GetActiveAgentCount();

        // Record all contributing agents' details
        Dictionary<int, float> allContributions = contributionTracker.GetAllContributions();

        float totalAgentReward = 0f;
        bool validAgentCount = (activeAgents == requiredAgents);
        bool isCollaborationSuccess = false;

        if (activeAgents == 0)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"Unexpected situation: {blockTypeName} reached the goal but no active agents were detected!");

                // Get block velocity
                Rigidbody blockRb = targetBlock.GetComponent<Rigidbody>();
                if (blockRb != null)
                {
                    Debug.LogWarning($"{blockTypeName} velocity: {blockRb.velocity.magnitude}, may have entered goal due to inertia or collision");
                }
            }

            // Negative reward for goal reached without agent contribution
            totalAgentReward = -5f;
        }
        else if (validAgentCount)
        {
            // Correct number of agents collaborated successfully
            totalAgentReward = rewardValue;
            isCollaborationSuccess = true;

            if (enableDebugLogs)
            {
                Debug.Log($"Successful collaboration! {blockTypeName} pushed into goal by {activeAgents} agents. Reward: {rewardValue}");

                // Print contribution details
                string contributionDetails = "";
                foreach (var entry in allContributions)
                {
                    contributionDetails += $"Agent {entry.Key}: Contribution {entry.Value:F2}, ";
                }

                Debug.Log($"Contributions: {contributionDetails}");
            }
        }
        else
        {
            // Incorrect number of agents, apply partial penalty
            totalAgentReward = -2f * (requiredAgents - activeAgents);

            if (enableDebugLogs)
            {
                Debug.Log($"Incomplete collaboration! {blockTypeName} pushed by {activeAgents}/{requiredAgents} agents. Penalty: {totalAgentReward}");
            }
        }

        // Apply reward
        m_AgentGroup.AddGroupReward(totalAgentReward);

        // Record successful contributions
        if (totalAgentReward > 0)
        {
            foreach (var agentId in contributionTracker.contributingAgents)
            {
                if (!successMemory.ContainsKey(agentId))
                    successMemory[agentId] = 0;
                successMemory[agentId] += totalAgentReward / activeAgents;
            }
        }

        // Reset contributions
        contributionTracker.ResetContributions();

        // Update remaining blocks
        m_RemainingBlocks--;
        if (m_RemainingBlocks <= 0)
        {
            // All blocks completed, end episode
            m_AgentGroup.EndGroupEpisode();
            ResetScene();
        }

        // Change ground material based on collaboration success
        if (isCollaborationSuccess)
        {
            if (m_PushBlockSettings.collaborationScoreMaterial != null)
            {
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.collaborationScoreMaterial, 0.5f));
            }
            else
            {
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.goalScoredMaterial, 0.5f));
            }
        }
        else
        {
            if (m_PushBlockSettings.failedCollaborationMaterial != null)
            {
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.failedCollaborationMaterial, 0.5f));
            }
            else if (m_PushBlockSettings.failMaterial != null)
            {
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.failMaterial, 0.5f));
            }
            else
            {
                StartCoroutine(GoalScoredSwapGroundMaterial(m_PushBlockSettings.goalScoredMaterial, 0.5f));
            }
        }
    }

    /// <summary>
    /// Reset the scene
    /// </summary>
    public void ResetScene()
    {
        m_ResetTimer = 0;
        // Randomly rotate the platform
        int rotationIndex = Random.Range(0, 4);
        area.transform.rotation = Quaternion.Euler(0f, 90f * rotationIndex, 0f);

        // Reset agent positions
        foreach (var agent in AgentsList)
        {
            agent.transform.position = GetRandomSpawnPos();
            agent.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var rb = agent.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Reset block positions
        m_RemainingBlocks = BlocksList.Count;
        foreach (var block in BlocksList)
        {
            block.gameObject.SetActive(true);
            block.transform.position = GetRandomSpawnPos();
            block.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);

            var rb = block.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Reset contributions for each block
            var contributionTracker = block.GetComponent<BlockContributionTracker>();
            if (contributionTracker != null)
            {
                contributionTracker.ResetContributions();
            }
        }
    }

    /// <summary>
    /// Method used with UnityEvent in GoalDetectTrigger
    /// Handles logic when block enters the goal
    /// </summary>
    public void OnBlockGoalTriggerEnter(Collider col, float score)
    {
        // Disable block after scoring
        col.gameObject.SetActive(false);

        // Get the block's ID script, like BlockTypeIdentifier
        var blockId = col.GetComponent<BlockTypeIdentifier>();
        if (blockId != null)
        {
            OnBlockGoalScored(blockId.blockType);
        }
    }

    /// <summary>
    /// Get a random position within ground bounds
    /// </summary>
    public Vector3 GetRandomSpawnPos()
    {
        var foundNewSpawnLocation = false;
        var randomSpawnPos = Vector3.zero;
        while (foundNewSpawnLocation == false)
        {
            var randomPosX = Random.Range(-areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier,
                areaBounds.extents.x * m_PushBlockSettings.spawnAreaMarginMultiplier);

            var randomPosZ = Random.Range(-areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier,
                areaBounds.extents.z * m_PushBlockSettings.spawnAreaMarginMultiplier);
            randomSpawnPos = ground.transform.position + new Vector3(randomPosX, 1f, randomPosZ);
            if (Physics.CheckBox(randomSpawnPos, new Vector3(1.5f, 0.01f, 1.5f)) == false)
            {
                foundNewSpawnLocation = true;
            }
        }
        return randomSpawnPos;
    }

    /// <summary>
    /// Coroutine to swap ground material for score visualization
    /// </summary>
    IEnumerator GoalScoredSwapGroundMaterial(Material mat, float time)
    {
        // Save original material
        Material originalMaterial = m_GroundRenderer.material;

        // Change to scored material
        m_GroundRenderer.material = mat;

        // Wait for the specified time
        yield return new WaitForSeconds(time);

        // Restore original material
        m_GroundRenderer.material = m_GroundMaterial;
    }
}
