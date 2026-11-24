using UnityEngine;
using System.Collections.Generic;

public enum Difficulty { Easy, Medium, Hard }



[System.Serializable]
public class DifficultySettings
{
    [field: SerializeField] public Difficulty Difficulty { get; private set; }
    [field: SerializeField] public WaveSet WaveSet { get; private set; }
    [field: SerializeField] public int StartingWax { get; private set; } = 400;
    [field: SerializeField] public int StartingFlame { get; private set; } = 100;
    [field: SerializeField] public float EnemySpeedMultiplier { get; private set; } = 1f;
    [field: SerializeField] public float EnemyHealthMultiplier { get; private set; } = 1f;
}

[CreateAssetMenu(menuName = "Candlepath/Map Definition")]
public class MapDefinition : ScriptableObject
{
    [field: SerializeField] public string DisplayName { get; private set; }
    [field: SerializeField] public string SceneName { get; private set; }
    [field: SerializeField] public Sprite Preview { get; private set; }
    [field: SerializeField] public AudioClip Music { get; private set; }
    [field: SerializeField] public AudioClip Ambience { get; private set; } // looped under the music (CandleSoundscape)

    [SerializeField] private List<DifficultySettings> difficulties = new List<DifficultySettings>();
    
    public IReadOnlyList<DifficultySettings> Difficulties => difficulties;

    public DifficultySettings GetDifficulty(Difficulty difficulty) =>
        difficulties.Find(d => d.Difficulty == difficulty);
}
