using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Advanced vision system with line-of-sight calculations and caching.
/// Provides efficient visibility checks between agents.
/// </summary>
public class VisionSystem : MonoBehaviour
{
    [Header("Settings")]
    public float defaultViewAngle = 135f;
    public float defaultViewDistance = 20f;
    public LayerMask occlusionLayers;

    [Header("Performance")]
    public bool useCache = true;
    public int cacheFrames = 3;

    // Visibility cache
    private Dictionary<(int, int), (bool visible, int frame)> visibilityCache =
        new Dictionary<(int, int), (bool visible, int frame)>();

    void LateUpdate()
    {
        // Clear old cache entries periodically
        if (Time.frameCount % 30 == 0)
        {
            CleanCache();
        }
    }

    /// <summary>
    /// Check if observer can see target with custom parameters
    /// </summary>
    public bool CanSee(Transform observer, Transform target, float viewAngle, float viewDistance)
    {
        if (observer == null || target == null) return false;

        // Cache check
        if (useCache)
        {
            var key = (observer.GetInstanceID(), target.GetInstanceID());
            if (visibilityCache.TryGetValue(key, out var cached))
            {
                if (Time.frameCount - cached.frame <= cacheFrames)
                {
                    return cached.visible;
                }
            }
        }

        bool result = PerformVisibilityCheck(observer, target, viewAngle, viewDistance);

        // Update cache
        if (useCache)
        {
            var key = (observer.GetInstanceID(), target.GetInstanceID());
            visibilityCache[key] = (result, Time.frameCount);
        }

        return result;
    }

    bool PerformVisibilityCheck(Transform observer, Transform target, float viewAngle, float viewDistance)
    {
        Vector3 toTarget = target.position - observer.position;
        float distance = toTarget.magnitude;

        // Distance check
        if (distance > viewDistance) return false;

        // Angle check (horizontal only)
        Vector3 toTargetFlat = new Vector3(toTarget.x, 0, toTarget.z);
        Vector3 forwardFlat = new Vector3(observer.forward.x, 0, observer.forward.z);

        float angle = Vector3.Angle(forwardFlat, toTargetFlat);
        if (angle > viewAngle / 2) return false;

        // Line of sight check with multiple rays for better accuracy
        return CheckLineOfSight(observer, target, distance);
    }

    bool CheckLineOfSight(Transform observer, Transform target, float distance)
    {
        Vector3 observerEye = observer.position + Vector3.up * 0.5f;

        // Check center point
        Vector3 targetCenter = target.position + Vector3.up * 0.5f;
        if (!IsBlocked(observerEye, targetCenter, distance))
            return true;

        // Check top and bottom points for partial visibility
        Vector3 targetTop = target.position + Vector3.up * 1.2f;
        Vector3 targetBottom = target.position + Vector3.up * 0.2f;

        if (!IsBlocked(observerEye, targetTop, distance))
            return true;

        if (!IsBlocked(observerEye, targetBottom, distance))
            return true;

        return false;
    }

    bool IsBlocked(Vector3 from, Vector3 to, float maxDistance)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;

        if (Physics.Raycast(from, direction.normalized, out RaycastHit hit, Mathf.Min(distance, maxDistance), occlusionLayers))
        {
            // Check if hit object is not the target
            return hit.distance < distance - 0.1f;
        }

        return false;
    }

    /// <summary>
    /// Get all visible targets from observer's perspective
    /// </summary>
    public List<Transform> GetVisibleTargets(Transform observer, List<Transform> potentialTargets,
        float viewAngle, float viewDistance)
    {
        List<Transform> visible = new List<Transform>();

        foreach (var target in potentialTargets)
        {
            if (target != observer && CanSee(observer, target, viewAngle, viewDistance))
            {
                visible.Add(target);
            }
        }

        return visible;
    }

    /// <summary>
    /// Calculate visibility percentage (how much of target is visible)
    /// </summary>
    public float GetVisibilityPercentage(Transform observer, Transform target, float viewDistance)
    {
        if (observer == null || target == null) return 0f;

        Vector3 toTarget = target.position - observer.position;
        float distance = toTarget.magnitude;

        if (distance > viewDistance) return 0f;

        Vector3 observerEye = observer.position + Vector3.up * 0.5f;

        // Check multiple points on target
        Vector3[] checkPoints = new Vector3[]
        {
            target.position + Vector3.up * 0.2f,  // Low
            target.position + Vector3.up * 0.5f,  // Center
            target.position + Vector3.up * 0.8f,  // Upper
            target.position + Vector3.up * 1.2f,  // Top
            target.position + Vector3.up * 0.5f + target.right * 0.3f,  // Right
            target.position + Vector3.up * 0.5f - target.right * 0.3f   // Left
        };

        int visibleCount = 0;
        foreach (var point in checkPoints)
        {
            if (!IsBlocked(observerEye, point, distance))
            {
                visibleCount++;
            }
        }

        return (float)visibleCount / checkPoints.Length;
    }

    void CleanCache()
    {
        List<(int, int)> toRemove = new List<(int, int)>();

        foreach (var kvp in visibilityCache)
        {
            if (Time.frameCount - kvp.Value.frame > cacheFrames * 10)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
        {
            visibilityCache.Remove(key);
        }
    }

    /// <summary>
    /// Debug visualization
    /// </summary>
    public void DrawViewCone(Transform observer, float viewAngle, float viewDistance, Color color)
    {
        if (observer == null) return;

        Vector3 pos = observer.position;
        Vector3 forward = observer.forward;

        // Draw arc
        int segments = 20;
        float halfAngle = viewAngle / 2;

        Vector3 prevPoint = pos;
        for (int i = 0; i <= segments; i++)
        {
            float angle = -halfAngle + (viewAngle * i / segments);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * forward;
            Vector3 point = pos + dir * viewDistance;

            Debug.DrawLine(prevPoint, point, color);
            if (i == 0 || i == segments)
            {
                Debug.DrawLine(pos, point, color);
            }
            prevPoint = point;
        }
    }
}
