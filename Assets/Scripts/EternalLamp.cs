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

    [Header("UI")]
    [SerializeField] private TMP_Text flameText;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMP_Text endTitle;

    public static bool IsGameOver { get; private set; }
    public int Flame { get; private set; }
    public int MaxFlame { get; private set; } = 100;

    private float baseMin, baseMax, flashTimer;
    private Color baseColor;

    void Awake()
    {
        IsGameOver = false;
        if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
        if (flicker == null) flicker = GetComponent<FlickerLight>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        baseMin = flicker.minIntensity;
        baseMax = flicker.maxIntensity;
        baseColor = spriteRenderer.color;
        if (endPanel != null) endPanel.SetActive(false);
    }

    void Start()
    {
        if (spawner.Settings != null) MaxFlame = spawner.Settings.StartingFlame;
        Flame = MaxFlame;

        Enemy.AnyReachedLamp += OnEnemyReachedLamp;
        spawner.AllWavesCleared += OnAllWavesCleared;
        Refresh();
    }

    void OnDestroy()
    {
        Enemy.AnyReachedLamp -= OnEnemyReachedLamp;
        if (spawner == null) return;
        spawner.AllWavesCleared -= OnAllWavesCleared;
    }

    void OnEnemyReachedLamp(Enemy enemy)
    {
        if (IsGameOver) return;
        Flame = Mathf.Max(0, Flame - enemy.LampDamage);
        flashTimer = hitFlashTime;
        Refresh();
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
        flicker.minIntensity = baseMin * t;
        flicker.maxIntensity = baseMax * t;
        if (flameText != null) flameText.text = $"Flame: {Flame}/{MaxFlame}";
    }

    void Update()
    {
        if (flashTimer > 0f) flashTimer -= Time.unscaledDeltaTime;
        spriteRenderer.color = flashTimer > 0f ? hitColor : baseColor;
    }

    void EndGame(string title)
    {
        IsGameOver = true;
        if (Flame == 0) { flicker.minIntensity = 0f; flicker.maxIntensity = 0f; }
        Time.timeScale = 0f;
        if (endTitle != null) endTitle.text = title;
        if (endPanel != null) endPanel.SetActive(true);
    }
}