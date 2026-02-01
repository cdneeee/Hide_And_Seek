using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Centralized grab/lock system for object manipulation.
/// Handles grab detection, holding physics, and lock mechanics.
/// </summary>
public class GrabSystem : MonoBehaviour
{
    [Header("Settings")]
    public float grabRange = 2.5f;
    public float holdDistance = 1.5f;
    public float holdHeight = 0.5f;
    public float holdSmoothing = 10f;

    [Header("Lock Settings")]
    public bool lockRequiresGrounded = true;
    public float lockCooldown = 0.5f;

    [Header("Physics")]
    public float throwForce = 5f;
    public bool inheritVelocity = true;

    // Track grab state
    private Dictionary<HideSeekAgent, GrabbableObject> agentGrabs =
        new Dictionary<HideSeekAgent, GrabbableObject>();

    private Dictionary<HideSeekAgent, float> lockCooldowns =
        new Dictionary<HideSeekAgent, float>();

    void Update()
    {
        // Update held object positions
        foreach (var kvp in agentGrabs)
        {
            if (kvp.Value != null && kvp.Key != null && kvp.Key.IsActive)
            {
                UpdateHeldObjectPosition(kvp.Key, kvp.Value);
            }
        }

        // Update cooldowns
        List<HideSeekAgent> toRemove = new List<HideSeekAgent>();
        foreach (var kvp in lockCooldowns)
        {
            if (Time.time > kvp.Value)
            {
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var agent in toRemove)
        {
            lockCooldowns.Remove(agent);
        }
    }

    /// <summary>
    /// Attempt to grab or release an object
    /// </summary>
    public bool TryGrabOrRelease(HideSeekAgent agent, List<GrabbableObject> objects)
    {
        if (agent == null || !agent.IsActive) return false;

        // If already holding something, release it
        if (agentGrabs.TryGetValue(agent, out GrabbableObject heldObject) && heldObject != null)
        {
            Release(agent, heldObject);
            return true;
        }

        // Try to grab nearest object
        GrabbableObject nearest = FindNearestGrabbable(agent, objects);
        if (nearest != null)
        {
            return TryGrab(agent, nearest);
        }

        return false;
    }

    /// <summary>
    /// Find the nearest grabbable object within range
    /// </summary>
    public GrabbableObject FindNearestGrabbable(HideSeekAgent agent, List<GrabbableObject> objects)
    {
        GrabbableObject nearest = null;
        float minDist = grabRange;

        foreach (var obj in objects)
        {
            if (obj.IsGrabbed || obj.IsLocked) continue;

            float dist = Vector3.Distance(agent.transform.position, obj.transform.position);
            if (dist < minDist)
            {
                // Additional check: object should be in front of agent
                Vector3 toObj = (obj.transform.position - agent.transform.position).normalized;
                float dot = Vector3.Dot(agent.transform.forward, toObj);

                if (dot > 0.3f) // Within ~70 degree cone in front
                {
                    minDist = dist;
                    nearest = obj;
                }
            }
        }

        return nearest;
    }

    bool TryGrab(HideSeekAgent agent, GrabbableObject obj)
    {
        if (obj.TryGrab(agent))
        {
            agentGrabs[agent] = obj;
            return true;
        }
        return false;
    }

    void Release(HideSeekAgent agent, GrabbableObject obj)
    {
        obj.Release();
        agentGrabs.Remove(agent);

        // Apply throw force based on agent movement
        if (inheritVelocity)
        {
            Rigidbody agentRb = agent.GetComponent<Rigidbody>();
            Rigidbody objRb = obj.GetComponent<Rigidbody>();

            if (agentRb != null && objRb != null)
            {
                Vector3 throwVel = agentRb.linearVelocity + agent.transform.forward * throwForce;
                objRb.linearVelocity = throwVel;
            }
        }
    }

    void UpdateHeldObjectPosition(HideSeekAgent agent, GrabbableObject obj)
    {
        Vector3 targetPos = agent.transform.position +
                           agent.transform.forward * holdDistance +
                           Vector3.up * holdHeight;

        obj.transform.position = Vector3.Lerp(
            obj.transform.position,
            targetPos,
            Time.deltaTime * holdSmoothing
        );

        // Rotate object to face same direction as agent
        Quaternion targetRot = Quaternion.LookRotation(agent.transform.forward);
        obj.transform.rotation = Quaternion.Slerp(
            obj.transform.rotation,
            targetRot,
            Time.deltaTime * holdSmoothing
        );
    }

    /// <summary>
    /// Attempt to lock or unlock an object
    /// </summary>
    public bool TryLockOrUnlock(HideSeekAgent agent, List<GrabbableObject> objects)
    {
        if (agent == null || !agent.IsActive) return false;

        // Check cooldown
        if (lockCooldowns.ContainsKey(agent)) return false;

        // Get target (held object or nearest)
        GrabbableObject target = null;

        if (agentGrabs.TryGetValue(agent, out GrabbableObject heldObject))
        {
            // Can't lock while holding
            return false;
        }

        target = FindNearestLockable(agent, objects);
        if (target == null) return false;

        if (target.IsLocked)
        {
            // Try to unlock (only if same team)
            if (target.LockingTeam == agent.Team)
            {
                target.Unlock();
                lockCooldowns[agent] = Time.time + lockCooldown;
                return true;
            }
        }
        else
        {
            // Lock the object
            if (CanLock(agent, target))
            {
                target.Lock(agent.Team);
                lockCooldowns[agent] = Time.time + lockCooldown;
                return true;
            }
        }

        return false;
    }

    GrabbableObject FindNearestLockable(HideSeekAgent agent, List<GrabbableObject> objects)
    {
        GrabbableObject nearest = null;
        float minDist = grabRange;

        foreach (var obj in objects)
        {
            if (obj.IsGrabbed) continue;

            float dist = Vector3.Distance(agent.transform.position, obj.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = obj;
            }
        }

        return nearest;
    }

    bool CanLock(HideSeekAgent agent, GrabbableObject obj)
    {
        if (obj.IsGrabbed) return false;

        if (lockRequiresGrounded)
        {
            // Check if object is on the ground
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.magnitude > 0.5f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Get the object currently held by an agent
    /// </summary>
    public GrabbableObject GetHeldObject(HideSeekAgent agent)
    {
        return agentGrabs.TryGetValue(agent, out GrabbableObject obj) ? obj : null;
    }

    /// <summary>
    /// Check if agent is holding anything
    /// </summary>
    public bool IsHolding(HideSeekAgent agent)
    {
        return agentGrabs.ContainsKey(agent) && agentGrabs[agent] != null;
    }

    /// <summary>
    /// Force release all objects (called on episode reset)
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var kvp in agentGrabs)
        {
            if (kvp.Value != null)
            {
                kvp.Value.Release();
            }
        }
        agentGrabs.Clear();
        lockCooldowns.Clear();
    }
}
