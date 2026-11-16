using System;
using UnityEngine;
using TMPro;

public class EternalLamp : MonoBehaviour
{
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private FlickerLight flicker;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitColor = new Color(0.6f, 0.3f, 1f);   // shadow-purple flash
    [SerializeField] private float hitFlashTime = 0.25f;
    [SerializeField, Range(0f, 1f)] private float minBrightness = 0.15f;   // light level just before it dies
    [Tooltip("Copies of the lamp at other path ends (sprite, light and FlickerLight, no EternalLamp): they dim and flash with this one")]
    [SerializeField] private FlickerLight[] otherEnds = new FlickerLight[0];

    [Header("UI")]
    [SerializeField] private TMP_Text flameText;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMP_Text endTitle;

    public static bool IsGameOver { get; private set; }
    public int Flame { get; private set; }
    public int MaxFlame { get; private set; } = 100;
    public event Action<int> Damaged; // flame lost

    private float flashTimer;

    // This lamp and its copies: the light dims and the sprite flashes on all of them
    private struct LampLight
    {
        public FlickerLight flicker;
        public SpriteRenderer sprite;
        public float baseMin, baseMax;
        public Color baseColor;
    }
    private LampLight[] lights;

    void Awake()
    {
        IsGameOver = false;
        if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
        if (flicker == null) flicker = GetComponent<FlickerLight>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        lights = new LampLight[otherEnds.Length + 1];
        for (int i = 0; i < lights.Length; i++)
        {
            var f = i == 0 ? flicker : otherEnds[i - 1];
            var sr = i == 0 ? spriteRenderer : f.GetComponent<SpriteRenderer>();
            lights[i] = new LampLight { flicker = f, sprite = sr, baseMin = f.minIntensity, baseMax = f.maxIntensity, baseColor = sr.color };
        }
        if (endPanel != null) endPanel.SetActive(false);
    }

    void Start()
    {
        if (spawner.Settings != null) MaxFlame = spawner.Settings.StartingFlame;
        Flame = MaxFlame;

        spawner.EnemyReachedLamp += OnEnemyReachedLamp;
        spawner.AllWavesCleared += OnAllWavesCleared;

        // The HUD strip replaces the plain flame text (GameHud switches itself off without map settings)
        if (flameText != null && spawner.Settings != null && FindFirstObjectByType<GameHud>() != null)
            flameText.gameObject.SetActive(false);
        Refresh();
    }

    void OnDestroy()
    {
        if (spawner == null) return;
        spawner.EnemyReachedLamp -= OnEnemyReachedLamp;
        spawner.AllWavesCleared -= OnAllWavesCleared;
    }

    void OnEnemyReachedLamp(Enemy enemy)
    {
        if (IsGameOver) return;
        int lost = Mathf.Min(Flame, enemy.LampDamage);
        Flame -= lost;
        flashTimer = hitFlashTime;
        Refresh();
        Damaged?.Invoke(lost);
        if (Flame == 0) EndGame("The Eternal Lamp went out");
    }

    void OnAllWavesCleared()
    {
        if (!IsGameOver) EndGame("The lamp survived the night!");
    }

    // State change: the lamp's light dims as it loses flame
    void Refresh()
    {
        float t = Mathf.Lerp(minBrightness, 1f, (float)Flame / MaxFlame);
        foreach (var l in lights)
        {
            l.flicker.minIntensity = l.baseMin * t;
            l.flicker.maxIntensity = l.baseMax * t;
        }
        if (flameText != null) flameText.text = $"Flame: {Flame}/{MaxFlame}";
    }

    void Update()
    {
        if (flashTimer > 0f) flashTimer -= Time.unscaledDeltaTime;
        foreach (var l in lights)
            l.sprite.color = flashTimer > 0f ? hitColor : l.baseColor;
    }

    void EndGame(string title)
    {
        IsGameOver = true;
        if (Flame == 0)
            foreach (var l in lights) { l.flicker.minIntensity = 0f; l.flicker.maxIntensity = 0f; }
        Time.timeScale = 0f;
        if (endTitle != null) endTitle.text = title;
        if (endPanel != null) endPanel.SetActive(true);
    }
}