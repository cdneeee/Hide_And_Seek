using UnityEngine;

public class FreeCamera : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 20f;
    public float fastMultiplier = 3f;

    [Header("Look")]
    public float lookSpeed = 3f;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private bool isControlling = false;

    void Start()
    {
        rotationX = transform.eulerAngles.y;
        rotationY = -transform.eulerAngles.x;
    }

    void Update()
    {
        // Right-click to control camera
        if (Input.GetMouseButtonDown(1))
        {
            isControlling = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isControlling = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (!isControlling) return;

        // Look
        rotationX += Input.GetAxis("Mouse X") * lookSpeed;
        rotationY += Input.GetAxis("Mouse Y") * lookSpeed;
        rotationY = Mathf.Clamp(rotationY, -90f, 90f);

        transform.rotation = Quaternion.Euler(-rotationY, rotationX, 0);

        // Movement
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

        transform.position += move.normalized * speed * Time.unscaledDeltaTime;
    }
}
