using UnityEngine;

// Every sound effect the game plays from code, in one place so clips and levels can be tuned in the Inspector.
// Lives at Resources/SfxLibrary (like CandleCatalog) so every map scene picks it up without extra wiring.
// Map ambience is set per map instead: MapDefinition.Ambience.
[CreateAssetMenu(menuName = "Candlepath/Sfx Library")]
public class SfxLibrary : ScriptableObject
{
    public const string ResourcePath = "SfxLibrary";

    [System.Serializable]
    public class Sound
    {
        [Tooltip("One is picked at random each time.")]
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch spread (0.1 = +-10%) so repeats don't sound identical.")]
        [Range(0f, 0.5f)] public float pitchJitter = 0.05f;
        [Tooltip("Shortest gap between two plays of this sound, in seconds (stops rapid repeats stacking up).")]
        [Min(0f)] public float minInterval = 0f;

        public AudioClip Pick() => clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
    }

    [Header("One-shots (Settings > Effects)")]
    [Tooltip("A candle is placed or relit.")]
    public Sound candleLit = new Sound();
    [Tooltip("A candle burns out.")]
    public Sound candleOut = new Sound();
    [Tooltip("A spark candle fires.")]
    public Sound sparkFired = new Sound();
    [Tooltip("An enemy is burned up by candlelight.")]
    public Sound enemyKilled = new Sound();
    [Tooltip("A flying enemy (Enemy.Flying, e.g. moth, wind spirit) is burned up; used instead of 'enemyKilled'.")]
    public Sound flyerKilled = new Sound();
    [Tooltip("An enemy reaches the Eternal Lamp and takes some of its flame.")]
    public Sound lampHit = new Sound();

    [Header("Loops")]
    [Tooltip("Soft burning while a few candles are lit (Ambient).")]
    public AudioClip burning;
    [Tooltip("Takes over from 'burning' as more candles are lit (Ambient).")]
    public AudioClip burningStrong;
    [Range(0f, 1f)] public float burningVolume = 0.35f;
    [Tooltip("Joins in while any candle is on its last stage before going out (Ambient).")]
    public AudioClip burningRapid;
    [Range(0f, 1f)] public float burningRapidVolume = 0.4f;
    [Tooltip("While a lantern's aura is burning Shadows (Effects).")]
    public AudioClip enemyBurning;
    [Range(0f, 1f)] public float enemyBurningVolume = 0.6f;
    [Tooltip("Level of the map's ambience track (MapDefinition.Ambience, Ambient).")]
    [Range(0f, 1f)] public float ambienceVolume = 0.5f;

    static SfxLibrary cached;
    public static SfxLibrary Instance => cached != null ? cached : (cached = Resources.Load<SfxLibrary>(ResourcePath));
}
