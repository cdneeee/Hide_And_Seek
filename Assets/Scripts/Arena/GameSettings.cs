using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "HideAndSeek/GameSettings")]
public class GameSettings : ScriptableObject
{
    [Header("Episode Settings")]
    public int maxEpisodeSteps = 500;
    [Range(0f, 1f)] public float gracePeriodFraction = 0.4f;

    [Header("Arena Settings")]
    public float arenaSize = 25f;
    public float wallHeight = 3f;

    [Header("Agent Settings")]
    public int numHiders = 2;
    public int numSeekers = 2;
    public float agentMoveSpeed = 5f;
    public float agentRotateSpeed = 200f;
    public float agentRadius = 0.5f;

    [Header("Object Settings")]
    public int numBoxes = 5;
    public int numRamps = 2;
    public float grabRange = 2.5f;
    public float objectSpacing = 3f;

    [Header("Vision Settings")]
    public float viewAngle = 135f;
    public float viewDistance = 20f;

    [Header("Reward Settings")]
    public float visibilityRewardScale = 1f;
    public float stepPenalty = -0.0001f;
    public float outOfBoundsPenalty = -0.01f;
    public float winReward = 1f;

    [Header("Capture Settings (Optional)")]
    public bool allowCapture = false;
    public float captureDistance = 1.2f;
    public int capturesToWin = 3;

    // Computed properties
    public int GracePeriodSteps => Mathf.RoundToInt(maxEpisodeSteps * gracePeriodFraction);
}
