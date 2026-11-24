using UnityEngine;

// Looping sounds that follow what's happening on the map (clips from SfxLibrary):
//   ambience      - the selected map's MapDefinition.Ambience                         (Settings > Ambient)
//   burning       - soft candle burning while candles are lit, handing over to a
//                   stronger crackle as more of them are lit                            (Ambient)
//   rapid burning - joins in while any candle is on its last stage before going out   (Ambient)
//   enemy burning - while a lantern's aura is burning Shadows                         (Effects)
// TowerBuilder adds this to the scene, the same way it adds NightLighting.
public class CandleSoundscape : MonoBehaviour
{
    const float FadeSpeed = 1.5f; // volume per second
    const int FewCandles = 3;     // soft burning reaches full level at this many lit candles
    const int ManyCandles = 10;   // the strong crackle has fully taken over at this many

    private SfxLibrary lib;
    private AudioSource ambience, burning, strong, rapid, enemyBurning;

    void Start()
    {
        lib = SfxLibrary.Instance;
        ambience = MakeLoop(GameSession.HasSelection ? GameSession.Map.Ambience : null);
        if (lib == null) return;
        burning = MakeLoop(lib.burning);
        strong = MakeLoop(lib.burningStrong);
        rapid = MakeLoop(lib.burningRapid);
        enemyBurning = MakeLoop(lib.enemyBurning);
    }

    AudioSource MakeLoop(AudioClip clip)
    {
        if (clip == null) return null;
        var src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f; // fades in from Update
        src.Play();
        return src;
    }

    void Update()
    {
        int lit = 0;
        bool dying = false;
        foreach (var candle in PlacedCandle.All)
        {
            if (candle.IsBurnedOut) continue;
            lit++;
            if (candle.Flicker != null && candle.Flicker.IsDying) dying = true;
        }

        bool burningShadows = false;
        foreach (var aura in CandleAura.All)
            if (aura.IsAttacking) { burningShadows = true; break; }

        // Gameplay sounds hush while paused or after the game ends; the room ambience carries on
        bool live = !PauseMenu.IsPaused && !EternalLamp.IsGameOver;
        float amb = GameSettings.Ambient;
        float fx = GameSettings.Sfx;

        Fade(ambience, (lib != null ? lib.ambienceVolume : 0.5f) * amb);
        if (lib == null) return;

        float few = Mathf.Clamp01(lit / (float)FewCandles);
        float many = Mathf.InverseLerp(FewCandles, ManyCandles, lit);
        Fade(burning, lib.burningVolume * few * (1f - many) * amb);
        Fade(strong, lib.burningVolume * many * amb);
        Fade(rapid, dying && live ? lib.burningRapidVolume * amb : 0f);
        Fade(enemyBurning, burningShadows && live ? lib.enemyBurningVolume * fx : 0f);
    }

    static void Fade(AudioSource src, float target)
    {
        if (src == null) return;
        src.volume = Mathf.MoveTowards(src.volume, target, FadeSpeed * Time.unscaledDeltaTime);
    }
}
