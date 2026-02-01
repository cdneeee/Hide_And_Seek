using UnityEngine;

public class GameTimeController : MonoBehaviour
{
    [Header("Time Settings")]
    [Range(0.01f, 2f)]
    public float timeScale = 1f;

    [Header("Controls")]
    public KeyCode pauseKey = KeyCode.Space;
    public KeyCode slowDownKey = KeyCode.Minus;
    public KeyCode speedUpKey = KeyCode.Equals;
    public KeyCode resetTimeKey = KeyCode.Alpha0;

    private float baseFixedDeltaTime;
    private bool isPaused = false;
    private float previousTimeScale = 1f;

    void Start()
    {
        // Store the default fixed delta time (usually 0.02 = 50 physics updates/sec)
        baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    void Update()
    {
        // Handle input
        if (Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }

        if (Input.GetKeyDown(slowDownKey))
        {
            timeScale = Mathf.Max(0.1f, timeScale - 0.1f);
        }

        if (Input.GetKeyDown(speedUpKey))
        {
            timeScale = Mathf.Min(2f, timeScale + 0.1f);
        }

        if (Input.GetKeyDown(resetTimeKey))
        {
            timeScale = 1f;
            isPaused = false;
        }

        // Apply time scale smoothly
        if (!isPaused)
        {
            ApplyTimeScale(timeScale);
        }
    }

    void ApplyTimeScale(float scale)
    {
        Time.timeScale = scale;
        // Scale fixedDeltaTime proportionally to maintain smooth physics
        Time.fixedDeltaTime = baseFixedDeltaTime * scale;
    }

    void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            ApplyTimeScale(timeScale);
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 160, 250, 120));
        GUILayout.Box("Time Control");

        if (isPaused)
        {
            GUILayout.Label("<color=yellow>PAUSED</color>", new GUIStyle(GUI.skin.label) { richText = true });
        }
        else
        {
            GUILayout.Label($"Time Scale: {timeScale:F2}x");
        }

        GUILayout.Label($"[Space] Pause/Resume");
        GUILayout.Label($"[-/+] Slow/Speed");
        GUILayout.Label($"[0] Reset to 1x");

        GUILayout.EndArea();
    }
}
