using UnityEngine;

/// <summary>Plays the selected map's looping background music (MapDefinition.Music).</summary>
[RequireComponent(typeof(AudioSource))]
public class MapMusic : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float volume = 0.4f;

    private AudioSource music;

    void UpdateVolume()
    {
        if (music != null) music.volume = volume * GameSettings.Music;
    }

    void OnDestroy() => GameSettings.Changed -= UpdateVolume;

    void Start()
    {
        AudioClip clip = GameSession.HasSelection ? GameSession.Map.Music : null;
        if (clip == null) return;

        var source = GetComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        music = source;
        source.volume = volume * GameSettings.Music;
        GameSettings.Changed += UpdateVolume;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.Play();
    }
}
