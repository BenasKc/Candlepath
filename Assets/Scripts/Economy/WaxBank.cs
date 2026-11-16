using System;
using UnityEngine;

// Wax is the currency: candles cost wax, killed enemies and finished waves pay it back.
// Starting amount comes from the map's DifficultySettings.StartingWax.
public class WaxBank : MonoBehaviour
{
    [SerializeField] private int fallbackStartingWax = 400; // used when no map/difficulty is selected

    public static WaxBank Instance { get; private set; }

    public int Wax { get; private set; }
    public event Action<int> Changed;           // new total
    public event Action<int> SpendRejected;     // amount that couldn't be paid

    private WaveSpawner spawner;

    void Awake()
    {
        Instance = this;
        spawner = FindFirstObjectByType<WaveSpawner>();
    }

    void Start()
    {
        Wax = spawner != null && spawner.Settings != null ? spawner.Settings.StartingWax : fallbackStartingWax;
        if (spawner != null)
        {
            spawner.EnemyKilled += OnEnemyKilled;
            spawner.WaveEnded += OnWaveEnded;
        }
        Changed?.Invoke(Wax);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (spawner == null) return;
        spawner.EnemyKilled -= OnEnemyKilled;
        spawner.WaveEnded -= OnWaveEnded;
    }

    void OnEnemyKilled(Enemy enemy) => Add(enemy.WaxReward);
    void OnWaveEnded(int wave, int bonus) => Add(bonus);

    public bool CanAfford(int amount) => Wax >= amount;

    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount))
        {
            NotifyCannotAfford(amount);
            return false;
        }
        Wax -= amount;
        Changed?.Invoke(Wax);
        return true;
    }

    public void NotifyCannotAfford(int amount) => SpendRejected?.Invoke(amount);

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Wax += amount;
        Changed?.Invoke(Wax);
    }
}
