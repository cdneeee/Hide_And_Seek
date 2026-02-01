using UnityEngine;

public class TimeController : MonoBehaviour
{
    [Range(0.01f, 1f)]
    public float timeScale = 1f;

    void Update()
    {
        Time.timeScale = timeScale;

        // Debug: Check if input is working
        if (Input.GetKey(KeyCode.W)) Debug.Log("W key detected!");
        if (Input.GetKey(KeyCode.A)) Debug.Log("A key detected!");
        if (Input.GetKey(KeyCode.S)) Debug.Log("S key detected!");
        if (Input.GetKey(KeyCode.D)) Debug.Log("D key detected!");
    }
}
