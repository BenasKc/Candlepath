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
    [Tooltip("Auto waves: seconds between clearing a wave and the next one starting")]
    [SerializeField] private float autoDelay = 5f;

    private DifficultySettings settings;

    public int CurrentWave { get; private set; } // 0 = none started yet
    public bool WaveRunning { get; private set; }
    public int TotalWaves => settings.WaveSet.Waves.Count;
    public DifficultySettings Settings => settings;
    public int EnemiesRemaining => toSpawn + alive;          // in the running wave: not yet spawned + on the path
    public int WaveEnemyCount { get; private set; }          // total enemies in the running wave
    public float AutoCountdown { get; private set; } = -1f;  // seconds until the next wave auto-starts; -1 = not counting
    public float AutoDelay => autoDelay;

    // Auto waves: each cleared wave is followed by the next after autoDelay. Turning it off cancels the
    // countdown, so the next wave waits for StartNextWave again. Kept across restarts in this session.
    public bool AutoContinue
    {
        get => autoContinue;
        set
        {
            autoContinue = value;
            if (!value) AutoCountdown = -1f;
            else if (!WaveRunning && CurrentWave > 0 && CurrentWave < TotalWaves) AutoCountdown = autoDelay;
        }
    }
    static bool autoContinue;

    // Candles only burn down while a wave is running, so the time between waves (thinking, or the auto
    // countdown) is free. Without wave settings, or in the sandbox, they burn all the time.
    public static bool CandlesBurn => BurnBetweenWaves || current == null || current.settings == null || current.WaveRunning;
    public static bool BurnBetweenWaves;
    static WaveSpawner current;

    public event Action<int> WaveStarted;
    public event Action<int, int> WaveEnded; // wave number, bonus wax
    public event Action AllWavesCleared;
    public event Action<Enemy> EnemyReachedLamp;
    public event Action<Enemy> EnemyKilled;

    private int alive;
    private int groupsSpawning;
    private int toSpawn;

    void OnEnable() => current = this;

    void OnDisable()
    {
        if (current == this) current = null;
    }

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

        AutoCountdown = -1f;
        CurrentWave++;
        WaveRunning = true;
        WaveEnemyCount = toSpawn = EnemyCount(CurrentWave);
        WaveStarted?.Invoke(CurrentWave);

        foreach (var group in settings.WaveSet.Waves[CurrentWave - 1].Groups)
            StartCoroutine(SpawnGroupRoutine(group));
    }

    void Update()
    {
        if (AutoCountdown < 0f) return;
        if (EternalLamp.IsGameOver)
        {
            AutoCountdown = -1f;
            return;
        }
        AutoCountdown -= Time.deltaTime; // scaled: stops while paused
        if (AutoCountdown <= 0f) StartNextWave();
    }

    // 1-based wave number
    public int EnemyCount(int wave)
    {
        int count = 0;
        foreach (var group in settings.WaveSet.Waves[wave - 1].Groups) count += group.Count;
        return count;
    }

    public int BonusWax(int wave) => settings.WaveSet.Waves[wave - 1].BonusWax;

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
        toSpawn--;
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
        else if (autoContinue)
            AutoCountdown = autoDelay;
    }
}