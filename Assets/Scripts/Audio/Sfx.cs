using System.Collections.Generic;
using UnityEngine;

// Fire-and-forget sound effects from a small pool of AudioSources (clips come from SfxLibrary).
// Level follows Settings > Effects (Master is applied through AudioListener.volume), and each sound is
// panned a little toward where it happens on screen.
public static class Sfx
{
    const int MaxVoices = 16;
    const float MaxPan = 0.5f;

    static GameObject host;
    static readonly List<AudioSource> voices = new List<AudioSource>();
    static readonly Dictionary<SfxLibrary.Sound, float> lastPlayed = new Dictionary<SfxLibrary.Sound, float>();

    public static void CandleLit(Vector3 at, float delay = 0f) => Play(SfxLibrary.Instance, l => l.candleLit, at, delay);
    public static void CandleOut(Vector3 at) => Play(SfxLibrary.Instance, l => l.candleOut, at, 0f);
    public static void SparkFired(Vector3 at) => Play(SfxLibrary.Instance, l => l.sparkFired, at, 0f);
    public static void EnemyKilled(Vector3 at, bool flying = false) =>
        Play(SfxLibrary.Instance, l => flying ? l.flyerKilled : l.enemyKilled, at, 0f);
    public static void LampHit(Vector3 at) => Play(SfxLibrary.Instance, l => l.lampHit, at, 0f);

    static void Play(SfxLibrary lib, System.Func<SfxLibrary, SfxLibrary.Sound> pick, Vector3 at, float delay)
    {
        if (lib != null) Play(pick(lib), at, delay);
    }

    public static void Play(SfxLibrary.Sound sound, Vector3 at, float delay = 0f)
    {
        AudioClip clip = sound?.Pick();
        if (clip == null) return;

        AudioSource voice = FreeVoice(); // also resets the timers after a scene change
        if (voice == null) return;       // every voice busy: skip rather than cut one off

        float now = Time.unscaledTime;
        if (sound.minInterval > 0f && lastPlayed.TryGetValue(sound, out float last) && now - last < sound.minInterval) return;
        lastPlayed[sound] = now;

        voice.clip = clip;
        voice.volume = sound.volume * GameSettings.Sfx;
        voice.pitch = 1f + Random.Range(-sound.pitchJitter, sound.pitchJitter);
        voice.panStereo = Pan(at);
        if (delay > 0f) voice.PlayDelayed(delay);
        else voice.Play();
    }

    static AudioSource FreeVoice()
    {
        if (host == null)
        {
            // First sound in this scene (the old host went with the previous scene)
            host = new GameObject("Sfx");
            voices.Clear();
            lastPlayed.Clear();
        }

        foreach (var v in voices)
            if (!v.isPlaying) return v;
        if (voices.Count >= MaxVoices) return null;

        var voice = host.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        voices.Add(voice);
        return voice;
    }

    static float Pan(Vector3 at)
    {
        Camera cam = Camera.main;
        if (cam == null) return 0f;
        float x = cam.WorldToViewportPoint(at).x; // 0 = left edge, 1 = right edge
        return Mathf.Clamp((x - 0.5f) * 2f * MaxPan, -MaxPan, MaxPan);
    }
}
