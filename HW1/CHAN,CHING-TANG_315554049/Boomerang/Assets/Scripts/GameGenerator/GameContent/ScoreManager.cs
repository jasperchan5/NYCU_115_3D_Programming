using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Score")]
    public int score;
    public int targetsDestroyed;
    public int collectiblesCollected;

    public System.Action OnScoreChanged;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddTargetScore(int value)
    {
        score += value;
        targetsDestroyed++;
        OnScoreChanged?.Invoke();
    }

    public void AddCollectibleScore(int value)
    {
        score += value;
        collectiblesCollected++;
        OnScoreChanged?.Invoke();
    }

    public void ResetScore()
    {
        score = 0;
        targetsDestroyed = 0;
        collectiblesCollected = 0;
        OnScoreChanged?.Invoke();
    }
}