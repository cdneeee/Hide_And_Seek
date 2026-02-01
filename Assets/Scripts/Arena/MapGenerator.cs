using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedural map generator for arena variations.
/// Generates random obstacle layouts to improve training generalization.
/// </summary>
public class MapGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public GameSettings settings;
    public bool generateObstacles = true;
    public int minObstacles = 2;
    public int maxObstacles = 6;

    [Header("Obstacle Prefabs")]
    public GameObject obstaclePrefab;  // Internal wall/barrier prefab

    [Header("Generation Patterns")]
    public MapPattern pattern = MapPattern.Random;

    private List<GameObject> generatedObstacles = new List<GameObject>();
    private Transform obstacleContainer;

    public enum MapPattern
    {
        Random,      // Random obstacle placement
        Quadrant,    // One obstacle in each quadrant
        Central,     // Obstacles around center
        Perimeter,   // Obstacles along walls
        Corridors    // Creates corridor-like paths
    }

    void Awake()
    {
        obstacleContainer = new GameObject("GeneratedObstacles").transform;
        obstacleContainer.SetParent(transform);
    }

    public void GenerateMap()
    {
        ClearGeneratedObstacles();

        if (!generateObstacles || obstaclePrefab == null) return;

        switch (pattern)
        {
            case MapPattern.Random:
                GenerateRandomObstacles();
                break;
            case MapPattern.Quadrant:
                GenerateQuadrantObstacles();
                break;
            case MapPattern.Central:
                GenerateCentralObstacles();
                break;
            case MapPattern.Perimeter:
                GeneratePerimeterObstacles();
                break;
            case MapPattern.Corridors:
                GenerateCorridorObstacles();
                break;
        }
    }

    void ClearGeneratedObstacles()
    {
        foreach (var obstacle in generatedObstacles)
        {
            if (obstacle != null)
                Destroy(obstacle);
        }
        generatedObstacles.Clear();
    }

    void GenerateRandomObstacles()
    {
        int count = Random.Range(minObstacles, maxObstacles + 1);
        float margin = 3f;
        float halfSize = settings.arenaSize / 2 - margin;

        List<Vector3> positions = new List<Vector3>();

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetValidObstaclePosition(positions, halfSize, 4f);
            if (pos != Vector3.zero)
            {
                positions.Add(pos);
                CreateObstacle(pos, GetRandomObstacleSize());
            }
        }
    }

    void GenerateQuadrantObstacles()
    {
        float offset = settings.arenaSize / 4;
        Vector3[] quadrants = new Vector3[]
        {
            new Vector3(-offset, 0, offset),   // Top-left
            new Vector3(offset, 0, offset),    // Top-right
            new Vector3(-offset, 0, -offset),  // Bottom-left
            new Vector3(offset, 0, -offset)    // Bottom-right
        };

        foreach (var center in quadrants)
        {
            if (Random.value > 0.3f) // 70% chance to place obstacle
            {
                Vector3 jitter = new Vector3(
                    Random.Range(-2f, 2f),
                    0,
                    Random.Range(-2f, 2f)
                );
                CreateObstacle(center + jitter, GetRandomObstacleSize());
            }
        }
    }

    void GenerateCentralObstacles()
    {
        int count = Random.Range(2, 5);
        float radius = settings.arenaSize / 6;

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count * i + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                Mathf.Cos(angle) * radius,
                0,
                Mathf.Sin(angle) * radius
            );
            CreateObstacle(pos, GetRandomObstacleSize());
        }
    }

    void GeneratePerimeterObstacles()
    {
        int count = Random.Range(minObstacles, maxObstacles + 1);
        float offset = settings.arenaSize / 2 - 4f;

        for (int i = 0; i < count; i++)
        {
            int side = Random.Range(0, 4);
            float along = Random.Range(-offset + 2f, offset - 2f);

            Vector3 pos = side switch
            {
                0 => new Vector3(along, 0, offset - 1f),   // North
                1 => new Vector3(along, 0, -offset + 1f),  // South
                2 => new Vector3(offset - 1f, 0, along),   // East
                _ => new Vector3(-offset + 1f, 0, along)   // West
            };

            CreateObstacle(pos, GetRandomObstacleSize());
        }
    }

    void GenerateCorridorObstacles()
    {
        // Create L-shaped or corridor-like structures
        bool horizontal = Random.value > 0.5f;
        float halfSize = settings.arenaSize / 2;

        if (horizontal)
        {
            // Horizontal corridor with gaps
            float z = Random.Range(-halfSize / 2, halfSize / 2);
            float segmentLength = settings.arenaSize / 3;

            CreateObstacle(
                new Vector3(-segmentLength, 0, z),
                new Vector3(segmentLength * 0.8f, settings.wallHeight * 0.7f, 1f)
            );
            CreateObstacle(
                new Vector3(segmentLength, 0, z),
                new Vector3(segmentLength * 0.8f, settings.wallHeight * 0.7f, 1f)
            );
        }
        else
        {
            // Vertical corridor with gaps
            float x = Random.Range(-halfSize / 2, halfSize / 2);
            float segmentLength = settings.arenaSize / 3;

            CreateObstacle(
                new Vector3(x, 0, -segmentLength),
                new Vector3(1f, settings.wallHeight * 0.7f, segmentLength * 0.8f)
            );
            CreateObstacle(
                new Vector3(x, 0, segmentLength),
                new Vector3(1f, settings.wallHeight * 0.7f, segmentLength * 0.8f)
            );
        }
    }

    Vector3 GetValidObstaclePosition(List<Vector3> existing, float halfSize, float minDist)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-halfSize, halfSize),
                0,
                Random.Range(-halfSize, halfSize)
            );

            bool valid = true;
            foreach (var existingPos in existing)
            {
                if (Vector3.Distance(pos, existingPos) < minDist)
                {
                    valid = false;
                    break;
                }
            }

            if (valid) return pos;
        }

        return Vector3.zero;
    }

    Vector3 GetRandomObstacleSize()
    {
        return new Vector3(
            Random.Range(2f, 5f),
            Random.Range(1.5f, settings.wallHeight),
            Random.Range(2f, 5f)
        );
    }

    void CreateObstacle(Vector3 position, Vector3 size)
    {
        GameObject obstacle = Instantiate(obstaclePrefab, obstacleContainer);
        obstacle.transform.localPosition = new Vector3(position.x, size.y / 2, position.z);
        obstacle.transform.localScale = size;
        obstacle.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

        obstacle.tag = "Wall";
        obstacle.layer = LayerMask.NameToLayer("Wall");

        generatedObstacles.Add(obstacle);
    }

    /// <summary>
    /// Call this at the start of each episode to randomize the map
    /// </summary>
    public void RandomizePattern()
    {
        pattern = (MapPattern)Random.Range(0, System.Enum.GetValues(typeof(MapPattern)).Length);
        GenerateMap();
    }
}
