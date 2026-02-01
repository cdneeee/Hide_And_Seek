using UnityEngine;
using System.Collections.Generic;

public class TrainingManager : MonoBehaviour
{
    [Header("Training Setup")]
    public GameObject arenaPrefab;
    public GameSettings settings;
    public int numArenas = 8;
    public float arenaSpacing = 60f;

    [Header("Layout")]
    public int arenasPerRow = 4;

    private List<ArenaController> arenas = new List<ArenaController>();

    void Start()
    {
        SpawnArenas();
    }

    void SpawnArenas()
    {
        for (int i = 0; i < numArenas; i++)
        {
            int row = i / arenasPerRow;
            int col = i % arenasPerRow;

            Vector3 position = new Vector3(
                col * arenaSpacing,
                0,
                row * arenaSpacing
            );

            GameObject arenaObj = Instantiate(arenaPrefab, position, Quaternion.identity);
            arenaObj.name = $"Arena_{i}";

            ArenaController controller = arenaObj.GetComponent<ArenaController>();
            if (controller != null)
            {
                controller.settings = settings;
                arenas.Add(controller);
            }
        }

        Debug.Log($"Spawned {arenas.Count} training arenas");
    }
}
