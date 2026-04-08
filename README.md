# Exploring Multi-Agent Based Human & AI Collaboration

**A Collaborative Push Block Environment with Customized Reward Functions for Multi-Agent Reinforcement Learning**

> UCL Bartlett School of Architecture · Architectural Computation – Digital Studio 1: Simulated Realities  
> Team: Du Hao, Gu Rui, Lu Haiyu, Pan Lingfeng · March 2025

---

## Overview

This project investigates **multi-agent collaboration** in a simulated environment using Unity ML-Agents. We built upon the standard PushBlock environment to explore how customized reward functions can encourage genuine cooperative behavior among AI agents — rather than independent, uncoordinated action.

The core challenge: three agents must push blocks of varying weights into a goal area, where heavier blocks physically require multiple agents to move. The default reward function treats each block-scoring event equally, providing no incentive for coordination. **Our contribution is designing and iterating on a collaboration-aware reward function** that explicitly rewards correct agent-matching and penalizes inefficient cooperation.

<p align="center">
  <img src="docs/default_vs_custom.png" alt="Default vs Customized Training Outcome" width="800"/>
</p>

---

## Table of Contents

- [Research Background](#research-background)
- [Environment Design](#environment-design)
- [Reward Function Design (My Contribution)](#reward-function-design-my-contribution)
- [Training & Optimization](#training--optimization)
- [Results](#results)
- [Project Structure](#project-structure)
- [How to Run](#how-to-run)
- [References](#references)

---

## Research Background

Human-AI collaboration is gaining attention across domains — from warehouse robotics (Amazon Robotics, NVIDIA Isaac Sim) to healthcare and emergency response. We reviewed three existing platforms for studying multi-agent cooperation:

| Platform | Extensible Envs | Real-time Interaction | Multiplayer | Open-sourced | Difficulty |
|----------|:---:|:---:|:---:|:---:|:---:|
| Overcooked | ✗ | ✓ | 1+1 | ✓ | Medium |
| CREW | ✓ | ✓ | No Limit | ✓ | Difficult |
| **ML-Agents** | **✓** | **✓** | **No Limit** | **✓** | **Easy** |

We chose **Unity ML-Agents** for its flexibility, extensibility, and native support for multi-agent reinforcement learning algorithms including **MA-POCA** (Multi-Agent POsthumous Credit Assignment).

### Key Literature

- Carroll et al. (2020) — *On the Utility of Learning about Humans for Human-AI Coordination* (Overcooked)
- Zhang et al. (2024) — *CREW: Facilitating Human-AI Teaming Research*
- Cohen et al. (2022) — *On the Use and Misuse of Absorbing States in Multi-agent RL*

---

## Environment Design

### PushBlockCollab Game

The environment features a bounded arena with a green **goal area** on one side. Three colored agents (Yellow, Blue, Purple) must push all blocks into the goal zone.

**Block Types:**

| Block | Weight | Required Agents | Label |
|-------|--------|:-:|:-:|
| Small (1) | Light | 1 | `BlockSmall` |
| Large (2) | Medium | 2 | `BlockLarge` |
| Very Large (3) | Heavy | 3 | `BlockVeryLarge` |

**Agent Constraints:**
- No inter-agent communication; each agent observes independently
- Equal strength and max speed across all agents
- Agents can only push (no lifting or passing through blocks)
- Movement is determined by facing direction

**Observation Space:** Grid-based (CNN visual perception) — each cell encodes object type via one-hot vectors. Tensor shape: `GridSize.x × GridSize.z × NumDetectableTags` (6 tags: Nothing, Wall, Agent, Goal, BlockSmall, BlockLarge, BlockVeryLarge).

**Action Space:** 7 discrete actions — do nothing, move forward/backward, rotate CW/CCW, strafe left/right.

**Episode Conditions:**
- Agents and blocks spawn at random non-overlapping positions each episode
- Episode ends when all blocks reach the goal area, or max steps are reached

---

## Reward Function Design (My Contribution)

### Problem with the Default Reward

The original PushBlockCollab environment uses a flat reward: every block scored yields the same `+1` group reward regardless of how many agents participated. This creates no incentive for cooperation — agents learn to act individually, often pushing blocks in wrong directions or getting stuck.

### Collaboration Hypothesis

I hypothesized that by **differentiating rewards based on block type and validating the number of contributing agents**, we could train agents to genuinely collaborate — converging on heavier blocks together rather than working in isolation.

### Reward Function: Piecewise Formulation

The customized reward function has two components:

#### 1. Collaboration Reward $R_{\text{collab}}$

$$R_{\text{collab}} = \begin{cases} -2.0, & A_{\text{active}} = 0 \\ R_{\max}, & A_{\text{active}} = A_{\text{required}} \\ -1.0 \times (A_{\text{required}} - A_{\text{active}}), & A_{\text{active}} < A_{\text{required}} \end{cases}$$

Where:
- $A_{\text{active}}$ = number of agents actually involved in pushing the block
- $A_{\text{required}}$ = minimum agents needed (1, 2, or 3 based on block type)
- $R_{\max}$ = maximum reward for successful collaboration

**Reward values per block type:**

| Block Type | Required Agents | $R_{\max}$ (Optimized 1&2) | $R_{\max}$ (Optimized 3&4) |
|:---:|:---:|:---:|:---:|
| Block3 (Very Large) | 3 | 50 | 5 |
| Block2 (Large) | 2 | 30 | 3 |
| Block1 (Small) | 1 | 20 | 1 |

#### 2. Time Penalty $R_{\text{time}}$

$$R_{\text{time}} = -\frac{0.05}{\text{MaxEnvironmentSteps}}$$

Applied every `FixedUpdate()` to encourage faster task completion.

#### Total Reward

$$R_{\text{total}} = R_{\text{collab}} + R_{\text{time}}$$

### Contribution Tracking

A critical implementation challenge was **detecting which agents actually contributed** to pushing a block into the goal. I developed a `BlockContributionTracker` system that:

1. Records agent-block collision forces (both initial contact and sustained contact)
2. Maintains a time-windowed history of contributions per agent
3. Applies decay to older contributions so only recent, active pushers count
4. Propagates indirect force when agents push each other into blocks

```csharp
// Core contribution logic (simplified)
public void AddAgentContribution(int agentId, float contributionValue)
{
    agentContributions[agentId] += contributionValue;
    lastContributionTime[agentId] = Time.time;
    contributingAgents.Add(agentId);
}

public int GetActiveAgentCount()
{
    // Count agents who contributed within the active time window
    return contributingAgents
        .Count(id => Time.time - lastContributionTime[id] < activeTimeWindow);
}
```

### Optimized Version: Flexible (Continuous) Reward Function

The piecewise formulation creates sharp thresholds — a "near miss" (e.g., 2 agents on a 3-agent block) gets the same penalty regardless of how close it was. I refined this to a **continuous formulation**:

$$R_{\text{collab}} = \frac{R_{\max} + (1 - 0.5 \times |A_{\text{required}} - A_{\text{active}}|)}{2}$$

This allows rewards to scale linearly with how close the agent count is to the target, providing smoother gradient signals for learning.

---

## Training & Optimization

### Algorithm: MA-POCA

We used **MA-POCA** (Multi-Agent POsthumous Credit Assignment), which features:
- Centralized critic with decentralized actors
- Attention-based policy network
- Policy optimization with clipping (PPO-style)
- Experience replay buffer

### Mass Tuning

Block mass significantly affects training dynamics. We iterated through multiple configurations:

| Version | Block1 Mass | Block2 Mass | Block3 Mass | Convergence |
|:---:|:---:|:---:|:---:|:---:|
| Original | 100 | 300 | 600 | Poor |
| Optimized 1 | 10 | 200 | 300 | ~6-10M steps |
| Optimized 2 | 10 | 100 | 150 | ~6-10M steps |
| **Optimized 3** | **10** | **40** | **90** | **~2-7M steps** |
| **Optimized 4** | **10** | **60** | **100** | **~2-7M steps** |

Reducing mass ratios between block types dramatically improved convergence speed and final reward levels.

### Cooperation Efficiency Analysis

A Python script (`Calculate DynamicEfficiency.py`) computes the **dynamic cooperation efficiency** — the running ratio of episodes where `Used == Required` agents:

```python
df['Efficient'] = df['Used'] == df['Required']
df['DynamicEfficiency'] = df['Efficient'].cumsum() / range(1, len(df) + 1)
```

---

## Results

### Training Curves (Reward & Entropy)

- **Group Cumulative Reward** increases over training, indicating improved collaboration
- **Policy Entropy** decreases as agents converge on effective cooperative strategies
- Optimized versions (3 & 4) achieve cumulative rewards of **~11** compared to **~3** for early versions
- Convergence achieved in **2-7 million steps** (Optimized 3&4) vs 6-10M+ (Optimized 1&2)

### Qualitative Observations

| Behavior | Default Reward | Customized Reward |
|----------|:-:|:-:|
| Agents push blocks independently | ✓ | Reduced |
| Agents converge on heavy blocks | Rare | Frequent |
| Blocks get stuck against walls | Common | Less common |
| All blocks cleared within episode | Inconsistent | Consistent |

---

## Project Structure

```
├── Code_Optimized 1 & 2/
│   ├── BlockContributionTracker.cs   # Tracks per-agent push contributions
│   ├── BlockTypeIdentifier.cs        # Enum identifier for block weight class
│   ├── GoalDetect.cs                 # Collision-based goal detection (single agent)
│   ├── GoalDetectTrigger.cs          # Trigger-based goal detection with UnityEvents
│   ├── PushAgentBasic.cs             # Basic single-agent push script
│   ├── PushAgentCollab.cs            # Collaborative agent with contribution recording
│   ├── PushBlockEnvController.cs     # Environment controller with custom reward logic
│   └── PushBlockSettings.cs          # Shared environment parameters
│
├── Code_Optimized 3 & 4/
│   ├── GoalDetectTrigger.cs          # Simplified trigger detection
│   ├── PushAgentCollab.cs            # Streamlined collaborative agent
│   ├── PushBlockEnvController.cs     # Refined env controller with flexible reward
│   └── PushBlockSettings.cs          # Simplified settings
│
├── Code_ Cooperation Efficiency/
│   └── Calculate DynamicEfficiency.py  # Post-training efficiency analysis
│
├── ProjectSlides.pdf                 # Presentation slides
└── README.md
```

---

## How to Run

### Prerequisites

- Unity 2021.3+ with [ML-Agents Toolkit](https://github.com/Unity-Technologies/ml-agents) (Release 20+)
- Python 3.8+ with `mlagents` package

### Setup

1. Clone this repository
2. Open the Unity project
3. Attach the appropriate scripts to agents and blocks in your scene:
   - `PushAgentCollab` → each agent GameObject
   - `BlockTypeIdentifier` → each block (set type accordingly)
   - `BlockContributionTracker` → each block (Optimized 1&2 only)
   - `GoalDetectTrigger` → each block (configure tag = "goal")
   - `PushBlockEnvController` → environment root
4. Configure training YAML (MA-POCA trainer)
5. Run training:
   ```bash
   mlagents-learn config/poca_pushblock.yaml --run-id=collab_v1
   ```

---

## Future Work

- Integrate **real-time human-guided feedback** (audio, discrete/continuous scalar signals)
- Incorporate **physiological data** (gaze tracking, EEG, ECG) for human-in-the-loop training
- Scale to more complex environments with dynamic agent creation/termination

---

## References

1. Juliani, A. et al. "Unity: A General Platform for Intelligent Agents." *arXiv:1809.02627*, 2020.
2. Cohen, A. et al. "On the Use and Misuse of Absorbing States in Multi-agent Reinforcement Learning." *arXiv:2111.05992*, 2022.
3. Zhang, L. et al. "CREW: Facilitating Human-AI Teaming Research." *arXiv:2408.00170*, 2024.
4. Carroll, M. et al. "On the Utility of Learning about Humans for Human-AI Coordination." *arXiv:1910.05789*, 2020.
5. Le Pelletier de Woillemont, P. et al. "Automated Play-Testing Through RL Based Human-Like Play-Styles Generation." *arXiv:2211.17188*, 2022.

---

## License

This project was developed as part of the UCL Bartlett Architectural Computation MSc program.
