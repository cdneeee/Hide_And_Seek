using UnityEngine;

/// <summary>
/// Handles agent movement physics with proper ground detection and slopes.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AgentMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotateSpeed = 200f;
    public float acceleration = 50f;
    public float deceleration = 30f;

    [Header("Ground Detection")]
    public float groundCheckDistance = 0.2f;
    public LayerMask groundLayers;
    public float maxSlopeAngle = 45f;

    [Header("Jump Settings (Optional)")]
    public bool allowJump = true;
    public float jumpForce = 5f;
    public float jumpCooldown = 0.5f;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 groundNormal;
    private float lastJumpTime;
    private Vector3 targetVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        CheckGround();
        ApplyMovement();
    }

    void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance + 0.1f, groundLayers))
        {
            isGrounded = true;
            groundNormal = hit.normal;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    /// <summary>
    /// Set movement input (called from agent's OnActionReceived)
    /// </summary>
    public void SetMoveInput(float horizontal, float vertical, float rotation)
    {
        // Calculate movement direction relative to facing
        Vector3 moveDir = transform.right * horizontal + transform.forward * vertical;
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);

        // Project onto ground plane for slopes
        if (isGrounded && groundNormal != Vector3.up)
        {
            moveDir = Vector3.ProjectOnPlane(moveDir, groundNormal).normalized * moveDir.magnitude;
        }

        targetVelocity = moveDir * moveSpeed;

        // Apply rotation
        transform.Rotate(0, rotation * rotateSpeed * Time.fixedDeltaTime, 0);
    }

    void ApplyMovement()
    {
        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        Vector3 targetHorizontalVel = new Vector3(targetVelocity.x, 0, targetVelocity.z);

        // Smooth acceleration/deceleration
        float accel = targetHorizontalVel.magnitude > 0.1f ? acceleration : deceleration;
        Vector3 newVelocity = Vector3.MoveTowards(currentHorizontalVel, targetHorizontalVel, accel * Time.fixedDeltaTime);

        // Preserve vertical velocity
        newVelocity.y = rb.linearVelocity.y;

        // Apply slope adjustment
        if (isGrounded && targetVelocity.magnitude > 0.1f)
        {
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle > 0 && slopeAngle < maxSlopeAngle)
            {
                // Add downward force on slopes to prevent floating
                newVelocity.y = Mathf.Min(newVelocity.y, -1f);
            }
        }

        rb.linearVelocity = newVelocity;
    }

    /// <summary>
    /// Attempt to jump
    /// </summary>
    public bool TryJump()
    {
        if (!allowJump) return false;
        if (!isGrounded) return false;
        if (Time.time - lastJumpTime < jumpCooldown) return false;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        lastJumpTime = Time.time;
        return true;
    }

    /// <summary>
    /// Stop all movement immediately
    /// </summary>
    public void Stop()
    {
        targetVelocity = Vector3.zero;
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    /// <summary>
    /// Freeze the agent (used during grace period)
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        rb.isKinematic = frozen;
        if (frozen)
        {
            targetVelocity = Vector3.zero;
        }
    }

    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal => groundNormal;
    public Vector3 Velocity => rb.linearVelocity;

    void OnDrawGizmosSelected()
    {
        // Ground check ray
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawLine(origin, origin + Vector3.down * (groundCheckDistance + 0.1f));

        // Velocity
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + (rb != null ? rb.linearVelocity : Vector3.zero));

        // Target velocity
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + targetVelocity);
    }
}
