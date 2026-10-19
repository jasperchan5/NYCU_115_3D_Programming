using UnityEngine;

public class LevelSetup : MonoBehaviour
{
    public BoundaryGenerator boundary;
    public SpawnManager manager;
    public PlayerSpawner spawner;

    void Start()
    {
        boundary.Generate();
        manager.SpawnAll();
        spawner.Spawn();
    }
}