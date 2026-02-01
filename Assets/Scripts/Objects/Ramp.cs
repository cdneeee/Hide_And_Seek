using UnityEngine;

/// <summary>
/// Specialized ramp behavior for hide and seek.
/// Ramps allow agents to climb over boxes and walls.
/// </summary>
[RequireComponent(typeof(GrabbableObject))]
public class Ramp : MonoBehaviour
{
    [Header("Ramp Properties")]
    public float length = 3f;
    public float height = 1f;
    public float width = 2f;
    public float maxClimbAngle = 45f;

    [Header("Physics")]
    public float mass = 5f;
    public PhysicsMaterial rampMaterial;

    [Header("Visual")]
    public Color defaultColor = new Color(0.6f, 0.4f, 0.2f);
    public Color usableColor = Color.green;

    private GrabbableObject grabbable;
    private Renderer rampRenderer;
    private MeshCollider meshCollider;

    void Awake()
    {
        grabbable = GetComponent<GrabbableObject>();
        rampRenderer = GetComponentInChildren<Renderer>();

        SetupCollider();
        SetupRigidbody();
    }

    void SetupCollider()
    {
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        meshCollider.convex = true;

        if (rampMaterial != null)
        {
            meshCollider.material = rampMaterial;
        }
    }

    void SetupRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = mass;
            rb.linearDamping = 2f;
            rb.angularDamping = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    /// <summary>
    /// Check if the ramp is positioned to be climbable
    /// </summary>
    public bool IsClimbable()
    {
        if (grabbable.IsGrabbed) return false;

        // Check angle relative to ground
        float angle = Vector3.Angle(transform.up, Vector3.up);
        return angle < maxClimbAngle && angle > 5f;
    }

    /// <summary>
    /// Get the climb direction (from low end to high end)
    /// </summary>
    public Vector3 GetClimbDirection()
    {
        // Assuming ramp's forward points uphill
        return transform.forward;
    }

    /// <summary>
    /// Get the entry point for climbing the ramp
    /// </summary>
    public Vector3 GetEntryPoint()
    {
        return transform.position - transform.forward * (length * 0.4f);
    }

    /// <summary>
    /// Get the exit point at the top of the ramp
    /// </summary>
    public Vector3 GetExitPoint()
    {
        return transform.position + transform.forward * (length * 0.4f) + Vector3.up * height;
    }

    /// <summary>
    /// Check if a position is on the ramp surface
    /// </summary>
    public bool IsOnRamp(Vector3 position)
    {
        Vector3 localPos = transform.InverseTransformPoint(position);

        // Check if within ramp bounds
        bool withinLength = Mathf.Abs(localPos.z) < length * 0.5f;
        bool withinWidth = Mathf.Abs(localPos.x) < width * 0.5f;
        bool aboveSurface = localPos.y > -0.1f && localPos.y < height + 0.5f;

        return withinLength && withinWidth && aboveSurface;
    }

    /// <summary>
    /// Check if ramp is leaning against something climbable (wall or box)
    /// </summary>
    public bool IsLeaningAgainstObstacle()
    {
        Vector3 topPoint = GetExitPoint();
        float checkRadius = 0.5f;

        Collider[] overlaps = Physics.OverlapSphere(topPoint, checkRadius);
        foreach (var col in overlaps)
        {
            if (col.gameObject != gameObject)
            {
                if (col.CompareTag("Wall") || col.GetComponent<Box>() != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    void Update()
    {
        // Visual feedback for usability
        if (rampRenderer != null)
        {
            bool usable = IsClimbable() && (IsLeaningAgainstObstacle() || grabbable.IsLocked);
            Color targetColor = usable ? usableColor : defaultColor;

            // Only update if not grabbed/locked (those have their own colors)
            if (!grabbable.IsGrabbed && !grabbable.IsLocked)
            {
                rampRenderer.material.color = Color.Lerp(
                    rampRenderer.material.color,
                    targetColor,
                    Time.deltaTime * 3f
                );
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        // Help agents climb by adding upward force
        HideSeekAgent agent = collision.gameObject.GetComponentInParent<HideSeekAgent>();
        if (agent != null && IsClimbable())
        {
            Rigidbody agentRb = agent.GetComponent<Rigidbody>();
            if (agentRb != null)
            {
                // Calculate climb assist force
                Vector3 climbDir = GetClimbDirection();
                float agentSpeed = Vector3.Dot(agentRb.linearVelocity, climbDir);

                if (agentSpeed > 0.1f)
                {
                    // Assist climbing
                    Vector3 assistForce = Vector3.up * 2f + climbDir * 0.5f;
                    agentRb.AddForce(assistForce, ForceMode.Acceleration);
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw entry and exit points
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(GetEntryPoint(), 0.2f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(GetExitPoint(), 0.2f);

        // Draw climb direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(GetEntryPoint(), GetExitPoint());

        // Draw leaning check area
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetExitPoint(), 0.5f);
    }
}
