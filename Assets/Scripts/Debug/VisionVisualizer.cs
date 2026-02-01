using UnityEngine;
using System.Collections.Generic;

public class VisionVisualizer : MonoBehaviour
{
    [Header("Settings")]
    public bool showVisionCones = true;
    public bool showDetectionRays = true;
    public int coneSegments = 20;
    public float coneAlpha = 0.15f;

    [Header("Colors")]
    public Color hiderConeColor = new Color(0.2f, 0.4f, 1f, 0.15f);
    public Color seekerConeColor = new Color(1f, 0.2f, 0.2f, 0.15f);
    public Color detectionRayColor = Color.red;
    public Color hiddenRayColor = Color.green;

    private ArenaController arena;
    private Material coneMaterial;
    private Material lineMaterial;

    void Start()
    {
        arena = FindObjectOfType<ArenaController>();
        CreateMaterials();
    }

    void CreateMaterials()
    {
        // Shader for transparent cone
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");

        coneMaterial = new Material(shader);
        coneMaterial.color = Color.white;

        // Line material
        lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    void OnRenderObject()
    {
        if (arena == null) return;

        if (showVisionCones)
        {
            DrawAllVisionCones();
        }

        if (showDetectionRays)
        {
            DrawDetectionRays();
        }
    }

    void DrawAllVisionCones()
    {
        // Draw hider cones
        foreach (var hider in arena.hiders)
        {
            if (hider != null && hider.IsActive)
            {
                DrawVisionCone(hider, hiderConeColor);
            }
        }

        // Draw seeker cones
        foreach (var seeker in arena.seekers)
        {
            if (seeker != null && seeker.IsActive)
            {
                DrawVisionCone(seeker, seekerConeColor);
            }
        }
    }

    void DrawVisionCone(HideSeekAgent agent, Color color)
    {
        if (arena.settings == null) return;

        float viewAngle = arena.settings.viewAngle;
        float viewDistance = arena.settings.viewDistance;
        Vector3 origin = agent.transform.position + Vector3.up * 0.1f;

        GL.PushMatrix();
        lineMaterial.SetPass(0);

        // Draw filled cone
        GL.Begin(GL.TRIANGLES);
        GL.Color(color);

        float halfAngle = viewAngle / 2f;
        float angleStep = viewAngle / coneSegments;

        for (int i = 0; i < coneSegments; i++)
        {
            float angle1 = -halfAngle + (i * angleStep);
            float angle2 = -halfAngle + ((i + 1) * angleStep);

            Vector3 dir1 = Quaternion.Euler(0, angle1, 0) * agent.transform.forward;
            Vector3 dir2 = Quaternion.Euler(0, angle2, 0) * agent.transform.forward;

            Vector3 point1 = origin + dir1 * viewDistance;
            Vector3 point2 = origin + dir2 * viewDistance;

            GL.Vertex(origin);
            GL.Vertex(point1);
            GL.Vertex(point2);
        }

        GL.End();

        // Draw outline
        GL.Begin(GL.LINES);
        Color outlineColor = new Color(color.r, color.g, color.b, 0.8f);
        GL.Color(outlineColor);

        // Left edge
        Vector3 leftDir = Quaternion.Euler(0, -halfAngle, 0) * agent.transform.forward;
        GL.Vertex(origin);
        GL.Vertex(origin + leftDir * viewDistance);

        // Right edge
        Vector3 rightDir = Quaternion.Euler(0, halfAngle, 0) * agent.transform.forward;
        GL.Vertex(origin);
        GL.Vertex(origin + rightDir * viewDistance);

        // Arc
        for (int i = 0; i < coneSegments; i++)
        {
            float angle1 = -halfAngle + (i * angleStep);
            float angle2 = -halfAngle + ((i + 1) * angleStep);

            Vector3 dir1 = Quaternion.Euler(0, angle1, 0) * agent.transform.forward;
            Vector3 dir2 = Quaternion.Euler(0, angle2, 0) * agent.transform.forward;

            GL.Vertex(origin + dir1 * viewDistance);
            GL.Vertex(origin + dir2 * viewDistance);
        }

        GL.End();
        GL.PopMatrix();
    }

    void DrawDetectionRays()
    {
        GL.PushMatrix();
        lineMaterial.SetPass(0);
        GL.Begin(GL.LINES);

        // Seekers detecting hiders
        foreach (var seeker in arena.seekers)
        {
            if (seeker == null || !seeker.IsActive) continue;

            foreach (var hider in arena.hiders)
            {
                if (hider == null || !hider.IsActive) continue;

                Vector3 seekerPos = seeker.transform.position + Vector3.up * 0.5f;
                Vector3 hiderPos = hider.transform.position + Vector3.up * 0.5f;

                if (seeker.CanSee(hider))
                {
                    // Red ray - seeker sees hider
                    GL.Color(detectionRayColor);
                    GL.Vertex(seekerPos);
                    GL.Vertex(hiderPos);

                    // Draw pulse effect at hider position
                    DrawPulse(hiderPos, detectionRayColor);
                }
            }
        }

        // Hiders detecting seekers (show as green - they see the threat)
        foreach (var hider in arena.hiders)
        {
            if (hider == null || !hider.IsActive) continue;

            foreach (var seeker in arena.seekers)
            {
                if (seeker == null || !seeker.IsActive) continue;

                if (hider.CanSee(seeker))
                {
                    Vector3 hiderPos = hider.transform.position + Vector3.up * 0.5f;
                    Vector3 seekerPos = seeker.transform.position + Vector3.up * 0.5f;

                    // Green ray - hider sees seeker (awareness)
                    GL.Color(hiddenRayColor);
                    GL.Vertex(hiderPos);
                    GL.Vertex(seekerPos);
                }
            }
        }

        GL.End();
        GL.PopMatrix();
    }

    void DrawPulse(Vector3 position, Color color)
    {
        // Draw a small diamond shape at detection point
        float size = 0.3f;
        GL.Color(color);

        GL.Vertex(position + Vector3.up * size);
        GL.Vertex(position + Vector3.right * size);

        GL.Vertex(position + Vector3.right * size);
        GL.Vertex(position - Vector3.up * size);

        GL.Vertex(position - Vector3.up * size);
        GL.Vertex(position - Vector3.right * size);

        GL.Vertex(position - Vector3.right * size);
        GL.Vertex(position + Vector3.up * size);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 290, 250, 100));
        GUILayout.Box("Vision Debug");

        showVisionCones = GUILayout.Toggle(showVisionCones, "Show Vision Cones");
        showDetectionRays = GUILayout.Toggle(showDetectionRays, "Show Detection Rays");

        GUILayout.Label("<color=red>Red ray</color> = Seeker sees Hider", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label("<color=green>Green ray</color> = Hider sees Seeker", new GUIStyle(GUI.skin.label) { richText = true });

        GUILayout.EndArea();
    }
}
