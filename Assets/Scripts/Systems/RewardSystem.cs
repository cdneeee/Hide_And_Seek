using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Centralized reward calculation system.
/// Handles visibility rewards, exploration bonuses, and team rewards.
/// </summary>
public class RewardSystem : MonoBehaviour
{
    [Header("References")]
    public ArenaController arena;
    public VisionSystem visionSystem;

    [Header("Reward Scales")]
    [Tooltip("Base reward/penalty for visibility")]
    public float visibilityReward = 0.01f;

    [Tooltip("Bonus for perfect hiding (no seeker sees you)")]
    public float perfectHideBonus = 0.005f;

    [Tooltip("Bonus for team coordination")]
    public float teamCoordinationBonus = 0.002f;

    [Tooltip("Penalty for staying still too long")]
    public float idlePenalty = -0.001f;

    [Tooltip("Reward for exploring new areas")]
    public float explorationReward = 0.001f;

    [Header("Exploration Tracking")]
    public float explorationGridSize = 2f;
    private Dictionary<HideSeekAgent, HashSet<Vector2Int>> visitedCells =
        new Dictionary<HideSeekAgent, HashSet<Vector2Int>>();

    [Header("Activity Tracking")]
    public float idleThreshold = 0.1f;
    public int idleFrameThreshold = 50;
    private Dictionary<HideSeekAgent, (Vector3 lastPos, int idleFrames)> activityTracking =
        new Dictionary<HideSeekAgent, (Vector3, int)>();

    public void Initialize()
    {
        if (arena == null)
            arena = GetComponent<ArenaController>();

        ResetTracking();
    }

    public void ResetTracking()
    {
        visitedCells.Clear();
        activityTracking.Clear();

        if (arena == null) return;

        foreach (var agent in arena.GetAllAgents())
        {
            visitedCells[agent] = new HashSet<Vector2Int>();
            activityTracking[agent] = (agent.transform.position, 0);
        }
    }

    /// <summary>
    /// Calculate and apply all rewards for the current step
    /// </summary>
    public void CalculateStepRewards()
    {
        if (arena == null || arena.IsGracePeriod) return;

        CalculateVisibilityRewards();
        CalculateActivityRewards();
        CalculateExplorationRewards();
        CalculateTeamRewards();
    }

    void CalculateVisibilityRewards()
    {
        var settings = arena.settings;

        // Track which hiders are seen by any seeker
        Dictionary<HideSeekAgent, bool> hiderVisibility = new Dictionary<HideSeekAgent, bool>();
        foreach (var hider in arena.hiders)
        {
            hiderVisibility[hider] = false;
        }

        // Calculate seeker rewards and track visibility
        foreach (var seeker in arena.seekers)
        {
            if (!seeker.IsActive) continue;

            int hidersInSight = 0;
            foreach (var hider in arena.hiders)
            {
                if (!hider.IsActive) continue;

                if (seeker.CanSee(hider))
                {
                    hidersInSight++;
                    hiderVisibility[hider] = true;
                }
            }

            // Reward proportional to hiders seen
            if (hidersInSight > 0)
            {
                seeker.AddReward(visibilityReward * hidersInSight);
            }
            else
            {
                seeker.AddReward(-visibilityReward);
            }
        }

        // Calculate hider rewards
        foreach (var hider in arena.hiders)
        {
            if (!hider.IsActive) continue;

            if (hiderVisibility[hider])
            {
                // Seen by at least one seeker
                hider.AddReward(-visibilityReward);
            }
            else
            {
                // Not seen by any seeker - bonus!
                hider.AddReward(visibilityReward + perfectHideBonus);
            }
        }
    }

    void CalculateActivityRewards()
    {
        foreach (var agent in arena.GetAllAgents())
        {
            if (!agent.IsActive) continue;

            var (lastPos, idleFrames) = activityTracking[agent];
            float movement = Vector3.Distance(agent.transform.position, lastPos);

            if (movement < idleThreshold)
            {
                idleFrames++;
                if (idleFrames > idleFrameThreshold)
                {
                    agent.AddReward(idlePenalty);
                }
            }
            else
            {
                idleFrames = 0;
            }

            activityTracking[agent] = (agent.transform.position, idleFrames);
        }
    }

    void CalculateExplorationRewards()
    {
        foreach (var agent in arena.GetAllAgents())
        {
            if (!agent.IsActive) continue;

            Vector2Int cell = GetGridCell(agent.transform.position);

            if (!visitedCells[agent].Contains(cell))
            {
                visitedCells[agent].Add(cell);
                agent.AddReward(explorationReward);
            }
        }
    }

    void CalculateTeamRewards()
    {
        // Reward hiders for staying close together (defensive clustering)
        if (arena.hiders.Count(h => h.IsActive) > 1)
        {
            var activeHiders = arena.hiders.Where(h => h.IsActive).ToList();
            float avgDistance = CalculateAverageTeamDistance(activeHiders);

            // Reward for being close but not too close
            float optimalDistance = 3f;
            float distanceScore = 1f - Mathf.Abs(avgDistance - optimalDistance) / 10f;
            distanceScore = Mathf.Clamp01(distanceScore);

            foreach (var hider in activeHiders)
            {
                hider.AddReward(teamCoordinationBonus * distanceScore);
            }
        }

        // Reward seekers for spreading out (search coverage)
        if (arena.seekers.Count(s => s.IsActive) > 1)
        {
            var activeSeekers = arena.seekers.Where(s => s.IsActive).ToList();
            float avgDistance = CalculateAverageTeamDistance(activeSeekers);

            // Reward for spreading out
            float spreadBonus = Mathf.Clamp01(avgDistance / 10f);

            foreach (var seeker in activeSeekers)
            {
                seeker.AddReward(teamCoordinationBonus * spreadBonus);
            }
        }
    }

    float CalculateAverageTeamDistance(List<HideSeekAgent> team)
    {
        if (team.Count < 2) return 0f;

        float totalDistance = 0f;
        int count = 0;

        for (int i = 0; i < team.Count; i++)
        {
            for (int j = i + 1; j < team.Count; j++)
            {
                totalDistance += Vector3.Distance(
                    team[i].transform.position,
                    team[j].transform.position
                );
                count++;
            }
        }

        return count > 0 ? totalDistance / count : 0f;
    }

    Vector2Int GetGridCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / explorationGridSize),
            Mathf.FloorToInt(position.z / explorationGridSize)
        );
    }

    /// <summary>
    /// Get exploration coverage percentage for an agent
    /// </summary>
    public float GetExplorationCoverage(HideSeekAgent agent)
    {
        if (!visitedCells.ContainsKey(agent)) return 0f;

        int totalCells = Mathf.CeilToInt(arena.settings.arenaSize / explorationGridSize);
        totalCells *= totalCells;

        return (float)visitedCells[agent].Count / totalCells;
    }
}
