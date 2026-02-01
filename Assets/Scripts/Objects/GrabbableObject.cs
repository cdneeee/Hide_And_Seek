using UnityEngine;

public enum ObjectType { Box, Ramp }

[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : MonoBehaviour
{
    [Header("Properties")]
    public ObjectType Type { get; private set; }
    public bool IsGrabbed { get; private set; }
    public bool IsLocked { get; private set; }
    public AgentTeam LockingTeam { get; private set; }

    private Rigidbody rb;
    private ArenaController arena;
    private HideSeekAgent grabbingAgent;
    private Renderer objectRenderer;
    private Color defaultColor;

    public void Initialize(ArenaController controller, ObjectType type)
    {
        arena = controller;
        Type = type;

        rb = GetComponent<Rigidbody>();
        rb.mass = type == ObjectType.Box ? 2f : 5f;
        rb.linearDamping = 2f;
        rb.angularDamping = 2f;
        // Never allow rotation - objects stay upright
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        objectRenderer = GetComponentInChildren<Renderer>();
        defaultColor = type == ObjectType.Box ? Color.gray : new Color(0.6f, 0.4f, 0.2f);
        objectRenderer.material.color = defaultColor;

        gameObject.tag = type.ToString();
        gameObject.name = $"{type}_{GetInstanceID()}";
    }

    public void ResetObject(Vector3 position)
    {
        // Reset state
        IsGrabbed = false;
        IsLocked = false;
        grabbingAgent = null;

        // Set position and rotation (ramps can have random Y rotation only)
        transform.localPosition = position;
        transform.localRotation = Type == ObjectType.Ramp ?
            Quaternion.Euler(0, Random.Range(0, 360), 0) : Quaternion.identity;

        // Reset physics (must not be kinematic to set velocity)
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.Sleep(); // Prevent immediate physics chaos

        if (objectRenderer != null)
            objectRenderer.material.color = defaultColor;
    }

    public bool TryGrab(HideSeekAgent agent)
    {
        if (IsGrabbed || IsLocked) return false;

        IsGrabbed = true;
        grabbingAgent = agent;
        // Keep physics active so collisions still work
        rb.useGravity = false;
        rb.linearDamping = 10f; // High damping for smooth follow
        // Freeze Y position and all rotation - objects slide on floor only
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        return true;
    }

    public void Release()
    {
        IsGrabbed = false;
        grabbingAgent = null;
        rb.useGravity = true;
        rb.linearDamping = 2f;
        // Keep rotation frozen always - objects should never flip
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public void MoveToward(Vector3 targetPosition)
    {
        if (!IsGrabbed || rb == null) return;

        // Only move on XZ plane - stay on floor
        Vector3 currentPos = transform.position;
        Vector3 targetXZ = new Vector3(targetPosition.x, currentPos.y, targetPosition.z);

        Vector3 direction = targetXZ - currentPos;
        rb.linearVelocity = direction * 15f;
    }

    public void Lock(AgentTeam team)
    {
        if (IsGrabbed) return;

        IsLocked = true;
        LockingTeam = team;
        rb.isKinematic = true;

        // Visual feedback
        objectRenderer.material.color = team == AgentTeam.Hider ?
            new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.3f, 0.3f);
    }

    public void Unlock()
    {
        IsLocked = false;
        rb.isKinematic = false;
        objectRenderer.material.color = defaultColor;
    }

    void OnCollisionStay(Collision collision)
    {
        // Allow agents to ride on moving objects (for box surfing)
        if (Type == ObjectType.Ramp || !rb.isKinematic)
        {
            HideSeekAgent agent = collision.gameObject.GetComponentInParent<HideSeekAgent>();
            if (agent != null && !rb.isKinematic)
            {
                // Transfer momentum
                agent.GetComponent<Rigidbody>().AddForce(
                    rb.linearVelocity * 0.3f, ForceMode.VelocityChange);
            }
        }
    }
}
