using UnityEngine;
using Unity.MLAgents;
using System.Collections.Generic;
using System.Linq;

public class ArenaController : MonoBehaviour
{
    [Header("Settings")]
    public GameSettings settings;

    [Header("Prefabs")]
    public GameObject hiderPrefab;
    public GameObject seekerPrefab;
    public GameObject boxPrefab;
    public GameObject rampPrefab;
    public GameObject wallPrefab;
    public GameObject floorPrefab;

    [Header("Runtime References")]
    public List<HideSeekAgent> hiders = new List<HideSeekAgent>();
    public List<HideSeekAgent> seekers = new List<HideSeekAgent>();
    public List<GrabbableObject> objects = new List<GrabbableObject>();

    [Header("Debug")]
    public bool showDebugInfo = true;

    // State
    private int currentStep;
    private bool isGracePeriod;
    private int captureCount;
    private Transform objectsContainer;
    private Transform agentsContainer;

    // Events
    public System.Action OnEpisodeStart;
    public System.Action OnGracePeriodEnd;
    public System.Action OnEpisodeEnd;

    void Awake()
    {
        CreateContainers();
        BuildArena();
        SpawnEntities();
    }

    void Start()
    {
        // Initialize arena at start
        ResetArena();
    }

    void FixedUpdate()
    {
        // ArenaController manages stepping, not individual agents
        Step();
    }

    void CreateContainers()
    {
        objectsContainer = new GameObject("Objects").transform;
        objectsContainer.SetParent(transform);

        agentsContainer = new GameObject("Agents").transform;
        agentsContainer.SetParent(transform);
    }

    void BuildArena()
    {
        // Floor
        GameObject floor = Instantiate(floorPrefab, transform);
        floor.transform.localPosition = Vector3.zero;
        floor.transform.localScale = new Vector3(settings.arenaSize, 0.1f, settings.arenaSize);

        // Walls
        CreateWall(new Vector3(0, settings.wallHeight / 2, settings.arenaSize / 2),
                   new Vector3(settings.arenaSize, settings.wallHeight, 0.5f)); // North
        CreateWall(new Vector3(0, settings.wallHeight / 2, -settings.arenaSize / 2),
                   new Vector3(settings.arenaSize, settings.wallHeight, 0.5f)); // South
        CreateWall(new Vector3(settings.arenaSize / 2, settings.wallHeight / 2, 0),
                   new Vector3(0.5f, settings.wallHeight, settings.arenaSize)); // East
        CreateWall(new Vector3(-settings.arenaSize / 2, settings.wallHeight / 2, 0),
                   new Vector3(0.5f, settings.wallHeight, settings.arenaSize)); // West
    }

    void CreateWall(Vector3 position, Vector3 scale)
    {
        GameObject wall = Instantiate(wallPrefab, transform);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;
        wall.tag = "Wall";
        wall.layer = LayerMask.NameToLayer("Wall");
    }

    void SpawnEntities()
    {
        // Spawn hiders
        for (int i = 0; i < settings.numHiders; i++)
        {
            GameObject hiderObj = Instantiate(hiderPrefab, agentsContainer);
            HideSeekAgent hider = hiderObj.GetComponent<HideSeekAgent>();
            hider.Initialize(this, AgentTeam.Hider, i);
            hiders.Add(hider);
        }

        // Spawn seekers
        for (int i = 0; i < settings.numSeekers; i++)
        {
            GameObject seekerObj = Instantiate(seekerPrefab, agentsContainer);
            HideSeekAgent seeker = seekerObj.GetComponent<HideSeekAgent>();
            seeker.Initialize(this, AgentTeam.Seeker, i);
            seekers.Add(seeker);
        }

        // Spawn boxes
        for (int i = 0; i < settings.numBoxes; i++)
        {
            GameObject boxObj = Instantiate(boxPrefab, objectsContainer);
            GrabbableObject box = boxObj.GetComponent<GrabbableObject>();
            box.Initialize(this, ObjectType.Box);
            objects.Add(box);
        }

        // Spawn ramps
        for (int i = 0; i < settings.numRamps; i++)
        {
            GameObject rampObj = Instantiate(rampPrefab, objectsContainer);
            GrabbableObject ramp = rampObj.GetComponent<GrabbableObject>();
            ramp.Initialize(this, ObjectType.Ramp);
            objects.Add(ramp);
        }
    }

    public void ResetArena()
    {
        currentStep = 0;
        isGracePeriod = true;
        captureCount = 0;

        // Reset all objects first
        List<Vector3> usedPositions = new List<Vector3>();
        foreach (var obj in objects)
        {
            Vector3 pos = GetValidSpawnPosition(usedPositions, settings.objectSpacing);
            usedPositions.Add(pos);
            obj.ResetObject(pos);
        }

        // Reset hiders (spawn in one half)
        foreach (var hider in hiders)
        {
            Vector3 pos = GetRandomPositionInHalf(true, usedPositions);
            usedPositions.Add(pos);
            hider.ResetAgent(pos);
            hider.SetFrozen(false);
        }

        // Reset seekers (spawn in other half, frozen during grace period)
        foreach (var seeker in seekers)
        {
            Vector3 pos = GetRandomPositionInHalf(false, usedPositions);
            usedPositions.Add(pos);
            seeker.ResetAgent(pos);
            seeker.SetFrozen(true);
        }

        OnEpisodeStart?.Invoke();
    }

    Vector3 GetValidSpawnPosition(List<Vector3> usedPositions, float minDistance)
    {
        int maxAttempts = 100;
        float margin = 3f;
        float halfSize = settings.arenaSize / 2 - margin;
        float spawnHeight = 1.5f; // Higher spawn to avoid floor clipping

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-halfSize, halfSize),
                spawnHeight,
                Random.Range(-halfSize, halfSize)
            );

            bool valid = true;
            foreach (var usedPos in usedPositions)
            {
                if (Vector3.Distance(pos, usedPos) < minDistance + 2f) // Extra spacing
                {
                    valid = false;
                    break;
                }
            }

            if (valid) return pos;
        }

        // Fallback
        return new Vector3(Random.Range(-halfSize, halfSize), spawnHeight, Random.Range(-halfSize, halfSize));
    }

    Vector3 GetRandomPositionInHalf(bool leftHalf, List<Vector3> usedPositions)
    {
        float margin = 3f;
        float halfSize = settings.arenaSize / 2 - margin;
        float spawnHeight = 1f; // Agent spawn height

        float minX = leftHalf ? -halfSize : margin;
        float maxX = leftHalf ? -margin : halfSize;

        for (int i = 0; i < 50; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(minX, maxX),
                spawnHeight,
                Random.Range(-halfSize, halfSize)
            );

            bool valid = true;
            foreach (var usedPos in usedPositions)
            {
                if (Vector3.Distance(pos, usedPos) < settings.agentRadius * 3)
                {
                    valid = false;
                    break;
                }
            }

            if (valid) return pos;
        }

        return new Vector3((minX + maxX) / 2, 1f, 0);
    }

    public void Step()
    {
        currentStep++;

        // End grace period
        if (isGracePeriod && currentStep >= settings.GracePeriodSteps)
        {
            EndGracePeriod();
        }

        // Calculate rewards
        if (!isGracePeriod)
        {
            CalculateVisibilityRewards();
        }

        // Check episode end
        if (currentStep >= settings.maxEpisodeSteps)
        {
            EndEpisode(false);
        }
    }

    void EndGracePeriod()
    {
        isGracePeriod = false;

        foreach (var seeker in seekers)
        {
            seeker.SetFrozen(false);
        }

        OnGracePeriodEnd?.Invoke();
    }

    void CalculateVisibilityRewards()
    {
        foreach (var hider in hiders)
        {
            if (!hider.IsActive) continue;

            bool isSeen = seekers.Any(s => s.IsActive && s.CanSee(hider));

            if (isSeen)
            {
                hider.AddReward(-settings.visibilityRewardScale * 0.01f);
            }
            else
            {
                hider.AddReward(settings.visibilityRewardScale * 0.01f);
            }
        }

        foreach (var seeker in seekers)
        {
            if (!seeker.IsActive) continue;

            bool seesAnyHider = hiders.Any(h => h.IsActive && seeker.CanSee(h));

            if (seesAnyHider)
            {
                seeker.AddReward(settings.visibilityRewardScale * 0.01f);
            }
            else
            {
                seeker.AddReward(-settings.visibilityRewardScale * 0.01f);
            }
        }
    }

    public void OnHiderCaptured(HideSeekAgent hider, HideSeekAgent seeker)
    {
        if (!settings.allowCapture) return;

        captureCount++;
        hider.OnCaptured();
        seeker.AddReward(settings.winReward * 0.5f);

        if (captureCount >= settings.capturesToWin)
        {
            EndEpisode(true);
        }
    }

    void EndEpisode(bool seekersWon)
    {
        // Final rewards
        float finalReward = settings.winReward;

        if (seekersWon)
        {
            foreach (var seeker in seekers)
                seeker.AddReward(finalReward);
            foreach (var hider in hiders)
                hider.AddReward(-finalReward);
        }
        else
        {
            // Hiders survived
            foreach (var hider in hiders)
                if (hider.IsActive) hider.AddReward(finalReward);
            foreach (var seeker in seekers)
                seeker.AddReward(-finalReward);
        }

        OnEpisodeEnd?.Invoke();

        // End all agent episodes
        foreach (var agent in hiders.Concat(seekers))
        {
            agent.EndEpisode();
        }

        // Reset arena for next episode
        ResetArena();
    }

    // Public accessors
    public bool IsGracePeriod => isGracePeriod;
    public int CurrentStep => currentStep;
    public float NormalizedTime => (float)currentStep / settings.maxEpisodeSteps;

    public List<HideSeekAgent> GetAllAgents() => hiders.Concat(seekers).ToList();
    public List<HideSeekAgent> GetTeammates(HideSeekAgent agent) =>
        agent.Team == AgentTeam.Hider ? hiders : seekers;
    public List<HideSeekAgent> GetOpponents(HideSeekAgent agent) =>
        agent.Team == AgentTeam.Hider ? seekers : hiders;

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 200, 150));
        GUILayout.Label($"Step: {currentStep}/{settings.maxEpisodeSteps}");
        GUILayout.Label($"Grace Period: {isGracePeriod}");
        GUILayout.Label($"Captures: {captureCount}");
        GUILayout.EndArea();
    }
}
