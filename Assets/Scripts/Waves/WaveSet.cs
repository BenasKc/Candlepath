using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Candlepath/Wave Set")]
public class WaveSet : ScriptableObject
{
    [SerializeField] private List<Wave> waves = new List<Wave>();
    public IReadOnlyList<Wave> Waves => waves;
}

[System.Serializable]
public class Wave
{
    [SerializeField] private List<SpawnGroup> groups = new List<SpawnGroup>();
    [field: SerializeField] public int BonusWax { get; private set; } = 100;

    public IReadOnlyList<SpawnGroup> Groups => groups;
}

[System.Serializable]
public class SpawnGroup
{
    [field: SerializeField] public Enemy EnemyPrefab { get; private set; }
    [field: SerializeField] public int Count { get; private set; } = 5;
    [field: SerializeField] public float Interval { get; private set; } = 0.8f;   // seconds between enemies
    [field: SerializeField] public float StartDelay { get; private set; } = 0f;   // seconds after wave start
    [field: SerializeField] public int SpawnPoint { get; private set; } = -1;     // -1 = random entrance
}