using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UIElements;

public class WaveSpawner : MonoBehaviour
{
    // [SerializeField] private MapDefinition map; UNUSED FOR NOW
    [SerializeField] private MapPath path;
    
#if UNITY_EDITOR
    [Header("Editor only: used when pressing Play directly in this scene")]
    [SerializeField] private MapDefinition editorTestMap;
    [SerializeField] private Difficulty editorTestDifficulty = Difficulty.Easy;
#endif
    private DifficultySettings settings;

    public int CurrentWave { get; private set; } // 0 = none started yet
    public bool WaveRunning { get; private set; }
    public int TotalWaves => settings.WaveSet.Waves.Count;
    public DifficultySettings Settings => settings;
    public event Action<int> WaveStarted;
    public event Action<int, int> WaveEnded; // wave number, bonus wax
    public event Action AllWavesCleared;
    public event Action<Enemy> EnemyReachedLamp;
    public event Action<Enemy> EnemyKilled;

    private int alive;
    private int groupsSpawning;

    void Awake()
    {
        if (path == null) path = FindFirstObjectByType<MapPath>();

        MapDefinition map = GameSession.Map;
        Difficulty difficulty = GameSession.Difficulty;

#if UNITY_EDITOR
        if (!GameSession.HasSelection && editorTestMap != null)
        {
            map = editorTestMap;
            difficulty = editorTestDifficulty;
            Debug.Log($"WaveSpawner: no menu selection, using editor test map '{map.DisplayName}' ({difficulty}).");
        }
#endif

        if (map == null)
        {
            Debug.LogError("WaveSpawner: no map selected. Start the game from the MainMenu scene.");
            enabled = false;
            return;
        }

        settings = map.GetDifficulty(difficulty);
    }
    

    public void StartNextWave()
    {
        if (WaveRunning || CurrentWave >= TotalWaves) return;

        CurrentWave++;
        WaveRunning = true;
        WaveStarted?.Invoke(CurrentWave);

        foreach (var group in settings.WaveSet.Waves[CurrentWave - 1].Groups)
            StartCoroutine(SpawnGroupRoutine(group));
    }

    IEnumerator SpawnGroupRoutine(SpawnGroup group)
    {
        groupsSpawning++;
        yield return new WaitForSeconds(group.StartDelay);
        for (int i = 0; i < group.Count; i++)
        {
            Spawn(group);
            yield return new WaitForSeconds(group.Interval);
        }

        groupsSpawning--;
        CheckWaveEnd();
    }

    void Spawn(SpawnGroup group)
    {
        var start = path.GetSpawn(group.SpawnPoint);
        var enemy = Instantiate(group.EnemyPrefab, start.transform.position, Quaternion.identity);
        
        foreach(var scaled in enemy.GetComponents<IDifficultyScaled>())
            scaled.ApplyDifficulty(settings);
        ;

        enemy.ReachedLamp += e => EnemyReachedLamp?.Invoke(e);
        enemy.Killed += e => EnemyKilled?.Invoke(e);
        enemy.Removed += _ => OnEnemyRemoved();
        enemy.Movement.Begin(start, settings.EnemySpeedMultiplier);
        alive++;
    }
    
    // This to be called on enemy death or lamp reach
    public void OnEnemyRemoved()
    {
        alive--;
        CheckWaveEnd();
    }

    void CheckWaveEnd()
    {
        if (!WaveRunning || alive > 0 || groupsSpawning > 0) return;
        WaveRunning = false;
        WaveEnded?.Invoke(CurrentWave, settings.WaveSet.Waves[CurrentWave - 1].BonusWax);
        if (CurrentWave >= TotalWaves)
            AllWavesCleared?.Invoke();
    }
}