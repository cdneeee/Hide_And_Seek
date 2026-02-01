using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public enum AgentTeam { Hider, Seeker }

[RequireComponent(typeof(Rigidbody))]
public class HideSeekAgent : Agent
{
    [Header("Identity")]
    public AgentTeam Team { get; private set; }
    public int AgentIndex { get; private set; }
    public bool IsActive { get; private set; } = true;

    [Header("Components")]
    private Rigidbody rb;
    private ArenaController arena;
    private GrabbableObject grabbedObject;

    [Header("State")]
    private bool isFrozen;
    private Vector3 spawnPosition;

    // Cached settings
    private float moveSpeed = 5f;
    private float rotateSpeed = 200f;
    private float grabRange = 2.5f;
    private float viewAngle = 135f;
    private float viewDistance = 20f;
    private bool isInitialized = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // Try to find arena if not set
        TryAutoInitialize();
    }

    void TryAutoInitialize()
    {
        if (isInitialized) return;

        // Try to find arena in parent hierarchy
        if (arena == null)
        {
            arena = GetComponentInParent<ArenaController>();
        }

        // If still no arena, try to find in scene
        if (arena == null)
        {
            arena = FindObjectOfType<ArenaController>();
        }
    }

    public void Initialize(ArenaController controller, AgentTeam team, int index)
    {
        arena = controller;
        Team = team;
        AgentIndex = index;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Cache settings
        if (arena.settings != null)
        {
            moveSpeed = arena.settings.agentMoveSpeed;
            rotateSpeed = arena.settings.agentRotateSpeed;
            grabRange = arena.settings.grabRange;
            viewAngle = arena.settings.viewAngle;
            viewDistance = arena.settings.viewDistance;
        }

        // Set color
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = team == AgentTeam.Hider ?
                new Color(0.2f, 0.4f, 1f) : new Color(1f, 0.2f, 0.2f);
        }

        gameObject.name = $"{team}_{index}";
        gameObject.tag = team.ToString();
        isInitialized = true;
    }

    public void ResetAgent(Vector3 position)
    {
        transform.localPosition = position;
        transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        spawnPosition = position;
        IsActive = true;
        isFrozen = false;

        if (grabbedObject != null)
        {
            grabbedObject.Release();
            grabbedObject = null;
        }
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        rb.isKinematic = frozen;
    }

    public override void OnEpisodeBegin()
    {
        // Arena controller handles resets - don't call from individual agents
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Skip if sensor not available (Space Size = 0 in Behavior Parameters)
        if (sensor == null) return;

            // Normalize positions to [-1, 1] range
            float normFactor = arena.settings.arenaSize / 2;
            if (normFactor == 0) normFactor = 12.5f; // Fallback

        // === Self observations (10 values) ===
        // Position (3)
        sensor.AddObservation(transform.localPosition / normFactor);

        // Forward direction (3)
        sensor.AddObservation(transform.forward);

        // Velocity (3)
        sensor.AddObservation(rb.linearVelocity / moveSpeed);

        // Holding object (1)
        sensor.AddObservation(grabbedObject != null ? 1f : 0f);

        // === Game state (3 values) ===
        sensor.AddObservation(arena.IsGracePeriod ? 1f : 0f);
        sensor.AddObservation(arena.NormalizedTime);
        sensor.AddObservation(Team == AgentTeam.Hider ? 1f : 0f);

        // === Teammate observations ===
        var teammates = arena.GetTeammates(this);
        if (teammates != null)
        {
            foreach (var teammate in teammates)
            {
                if (teammate == null || teammate == this) continue;
                AddAgentObservation(sensor, teammate, normFactor);
            }
        }
        // Pad if fewer teammates
        int maxTeammates = Mathf.Max(arena.settings.numHiders, arena.settings.numSeekers) - 1;
        int teammateCount = teammates != null ? teammates.Count - 1 : 0;
        for (int i = teammateCount; i < maxTeammates; i++)
        {
            sensor.AddObservation(new float[7]); // Padding
        }

        // === Opponent observations ===
        var opponents = arena.GetOpponents(this);
        if (opponents != null)
        {
            foreach (var opponent in opponents)
            {
                if (opponent == null) continue;
                AddAgentObservation(sensor, opponent, normFactor);
            }
        }
        // Pad if fewer opponents
        int maxOpponents = Mathf.Max(arena.settings.numHiders, arena.settings.numSeekers);
        int opponentCount = opponents != null ? opponents.Count : 0;
        for (int i = opponentCount; i < maxOpponents; i++)
        {
            sensor.AddObservation(new float[7]); // Padding
        }

        // === Object observations ===
        if (arena.objects != null)
        {
            foreach (var obj in arena.objects)
            {
                if (obj == null) continue;
                AddObjectObservation(sensor, obj, normFactor);
            }
        }
    }

    void AddEmptyObservations(VectorSensor sensor)
    {
        // Add zeros when not initialized
        for (int i = 0; i < 50; i++)
            sensor.AddObservation(0f);
    }

    void AddAgentObservation(VectorSensor sensor, HideSeekAgent other, float normFactor)
    {
        // Relative position (3)
        Vector3 relPos = (other.transform.localPosition - transform.localPosition) / normFactor;
        sensor.AddObservation(relPos);

        // Forward direction (3)
        sensor.AddObservation(other.transform.forward);

        // Is active (1)
        sensor.AddObservation(other.IsActive ? 1f : 0f);
    }

    void AddObjectObservation(VectorSensor sensor, GrabbableObject obj, float normFactor)
    {
        // Relative position (3)
        Vector3 relPos = (obj.transform.localPosition - transform.localPosition) / normFactor;
        sensor.AddObservation(relPos);

        // Object state (4)
        sensor.AddObservation(obj.IsGrabbed ? 1f : 0f);
        sensor.AddObservation(obj.IsLocked ? 1f : 0f);
        sensor.AddObservation(obj.LockingTeam == Team ? 1f : 0f);
        sensor.AddObservation(obj.Type == ObjectType.Ramp ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isInitialized || arena == null)
        {
            TryAutoInitialize();
        }

        if (arena == null || !IsActive || isFrozen || rb == null) return;

        // === Continuous Actions ===
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float rotate = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        // Movement (relative to agent facing direction)
        Vector3 moveDir = transform.right * moveX + transform.forward * moveZ;
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);

        Vector3 targetVelocity = moveDir * moveSpeed;
        Vector3 velocityChange = targetVelocity - new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(velocityChange, ForceMode.VelocityChange);

        // Rotation
        transform.Rotate(0, rotate * rotateSpeed * Time.fixedDeltaTime, 0);

        // === Discrete Actions ===
        int grabAction = actions.DiscreteActions[0]; // 0: nothing, 1: grab/release
        int lockAction = actions.DiscreteActions[1]; // 0: nothing, 1: lock/unlock

        if (grabAction == 1) ExecuteGrab();
        if (lockAction == 1) ExecuteLock();

        // Move grabbed object using physics (respects collisions, stays on floor)
        if (grabbedObject != null)
        {
            Vector3 holdPos = transform.position + transform.forward * 1.5f;
            grabbedObject.MoveToward(holdPos);
        }

        // Step penalty
        AddReward(arena.settings.stepPenalty);

        // Out of bounds check
        CheckBounds();

        // Capture check
        if (arena.settings.allowCapture && Team == AgentTeam.Seeker)
        {
            CheckCapture();
        }
    }

    void ExecuteGrab()
    {
        if (grabbedObject != null)
        {
            // Release current object
            grabbedObject.Release();
            grabbedObject = null;
        }
        else
        {
            // Try to grab nearest object
            GrabbableObject nearest = FindNearestGrabbable();
            if (nearest != null && nearest.TryGrab(this))
            {
                grabbedObject = nearest;
            }
        }
    }

    void ExecuteLock()
    {
        GrabbableObject target = grabbedObject ?? FindNearestGrabbable();
        if (target == null) return;

        if (target.IsLocked)
        {
            // Can only unlock if same team locked it
            if (target.LockingTeam == Team)
            {
                target.Unlock();
            }
        }
        else if (!target.IsGrabbed)
        {
            target.Lock(Team);
        }
    }

    GrabbableObject FindNearestGrabbable()
    {
        GrabbableObject nearest = null;
        float minDist = grabRange;

        foreach (var obj in arena.objects)
        {
            if (obj.IsGrabbed && obj != grabbedObject) continue;

            float dist = Vector3.Distance(transform.position, obj.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = obj;
            }
        }

        return nearest;
    }

    void CheckBounds()
    {
        float halfSize = arena.settings.arenaSize / 2;
        Vector3 pos = transform.localPosition;

        // Check if fallen through floor or too high
        if (pos.y < -1f || pos.y > 20f)
        {
            // Respawn at a safe position
            AddReward(arena.settings.outOfBoundsPenalty);
            RespawnSafe();
            return;
        }

        if (Mathf.Abs(pos.x) > halfSize || Mathf.Abs(pos.z) > halfSize)
        {
            AddReward(arena.settings.outOfBoundsPenalty);

            // Push back into bounds
            pos.x = Mathf.Clamp(pos.x, -halfSize, halfSize);
            pos.z = Mathf.Clamp(pos.z, -halfSize, halfSize);
            transform.localPosition = pos;
        }
    }

    void RespawnSafe()
    {
        // Release any grabbed object
        if (grabbedObject != null)
        {
            grabbedObject.Release();
            grabbedObject = null;
        }

        // Find a safe spawn position
        float margin = 3f;
        float halfSize = arena.settings.arenaSize / 2 - margin;

        Vector3 safePos = new Vector3(
            Random.Range(-halfSize, halfSize),
            1.5f,
            Random.Range(-halfSize, halfSize)
        );

        transform.localPosition = safePos;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void CheckCapture()
    {
        if (arena.IsGracePeriod) return;

        foreach (var hider in arena.hiders)
        {
            if (!hider.IsActive) continue;

            float dist = Vector3.Distance(transform.position, hider.transform.position);
            if (dist < arena.settings.captureDistance)
            {
                arena.OnHiderCaptured(hider, this);
            }
        }
    }

    public bool CanSee(HideSeekAgent other)
    {
        if (!other.IsActive) return false;

        Vector3 toOther = other.transform.position - transform.position;
        float distance = toOther.magnitude;

        // Distance check
        if (distance > viewDistance) return false;

        // Angle check
        float angle = Vector3.Angle(transform.forward, toOther);
        if (angle > viewAngle / 2) return false;

        // Line of sight check (raycast)
        Vector3 eyePos = transform.position + Vector3.up * 0.5f;
        Vector3 targetPos = other.transform.position + Vector3.up * 0.5f;
        Vector3 direction = targetPos - eyePos;

        if (Physics.Raycast(eyePos, direction.normalized, out RaycastHit hit, distance))
        {
            // Check if we hit the target agent
            HideSeekAgent hitAgent = hit.collider.GetComponentInParent<HideSeekAgent>();
            return hitAgent == other;
        }

        return true; // No obstruction
    }

    public void OnCaptured()
    {
        IsActive = false;
        gameObject.SetActive(false);
        AddReward(-arena.settings.winReward * 0.5f);
    }

    // Input buffering for discrete actions (GetKeyDown can be missed between decisions)
    private bool grabPressed = false;
    private bool lockPressed = false;

    void Update()
    {
        // Buffer key presses between decisions
        if (Input.GetKeyDown(KeyCode.G)) grabPressed = true;
        if (Input.GetKeyDown(KeyCode.L)) lockPressed = true;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manual control for testing - uses arrow keys to avoid conflict with FreeCamera
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetKey(KeyCode.RightArrow) ? 1f : Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f;
        continuous[1] = Input.GetKey(KeyCode.UpArrow) ? 1f : Input.GetKey(KeyCode.DownArrow) ? -1f : 0f;
        continuous[2] = Input.GetKey(KeyCode.Period) ? 1f : Input.GetKey(KeyCode.Comma) ? -1f : 0f;

        var discrete = actionsOut.DiscreteActions;
        discrete[0] = grabPressed ? 1 : 0;
        discrete[1] = lockPressed ? 1 : 0;

        // Clear buffers after use
        grabPressed = false;
        lockPressed = false;
    }

    void OnDrawGizmosSelected()
    {
        // View cone
        Gizmos.color = Team == AgentTeam.Hider ? Color.blue : Color.red;

        Vector3 leftBound = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward;

        Gizmos.DrawLine(transform.position, transform.position + leftBound * viewDistance);
        Gizmos.DrawLine(transform.position, transform.position + rightBound * viewDistance);
        Gizmos.DrawLine(transform.position + leftBound * viewDistance,
                        transform.position + rightBound * viewDistance);

        // Grab range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, grabRange);
    }
}
