using UnityEngine;

/// <summary>
/// Specialized box behavior for hide and seek.
/// Boxes are used to build shelters and block line of sight.
/// </summary>
[RequireComponent(typeof(GrabbableObject))]
public class Box : MonoBehaviour
{
    [Header("Box Properties")]
    public Vector3 defaultSize = new Vector3(2f, 1f, 2f);
    public float stackHeight = 1.1f;

    [Header("Physics")]
    public float mass = 2f;
    public PhysicsMaterial physicMaterial;

    [Header("Visual")]
    public Color defaultColor = Color.gray;
    public Color highlightColor = Color.yellow;

    private GrabbableObject grabbable;
    private Renderer boxRenderer;
    private bool isHighlighted;

    void Awake()
    {
        grabbable = GetComponent<GrabbableObject>();
        boxRenderer = GetComponentInChildren<Renderer>();

        SetupCollider();
        SetupRigidbody();
    }

    void SetupCollider()
    {
        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider>();
        }
        collider.size = Vector3.one; // Use transform.localScale for actual size
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
    /// Check if another box can stack on top of this one
    /// </summary>
    public bool CanStackOn()
    {
        if (grabbable.IsGrabbed) return false;

        // Check if there's space above
        Vector3 checkPos = transform.position + Vector3.up * stackHeight;
        float checkRadius = Mathf.Min(transform.localScale.x, transform.localScale.z) * 0.4f;

        Collider[] overlaps = Physics.OverlapSphere(checkPos, checkRadius);
        foreach (var col in overlaps)
        {
            if (col.gameObject != gameObject && col.GetComponent<Box>() != null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Get the position for stacking another box on top
    /// </summary>
    public Vector3 GetStackPosition()
    {
        return transform.position + Vector3.up * (transform.localScale.y + 0.5f);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (isHighlighted == highlighted) return;

        isHighlighted = highlighted;
        if (boxRenderer != null)
        {
            boxRenderer.material.color = highlighted ? highlightColor : defaultColor;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Detect stacking events
        Box otherBox = collision.gameObject.GetComponent<Box>();
        if (otherBox != null)
        {
            // Check if this box landed on top
            if (collision.GetContact(0).normal.y < -0.5f)
            {
                OnStackedOnBox(otherBox);
            }
        }
    }

    void OnStackedOnBox(Box bottomBox)
    {
        // Could add events or rewards for successful stacking
        // Debug.Log($"{gameObject.name} stacked on {bottomBox.gameObject.name}");
    }

    void OnDrawGizmosSelected()
    {
        // Show stack detection area
        Gizmos.color = Color.cyan;
        Vector3 checkPos = transform.position + Vector3.up * stackHeight;
        float checkRadius = Mathf.Min(transform.localScale.x, transform.localScale.z) * 0.4f;
        Gizmos.DrawWireSphere(checkPos, checkRadius);
    }
}
