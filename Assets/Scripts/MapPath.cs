using UnityEngine;

public class MapPath : MonoBehaviour
{
    [SerializeField] private  PathNode[] spawnPoints;

    public PathNode GetSpawn(int index)
    {
        if (index < 0 || index >= spawnPoints.Length)
            index = Random.Range(0, spawnPoints.Length);
        return spawnPoints[index];
    }
}
