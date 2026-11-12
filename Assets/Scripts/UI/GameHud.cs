using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiKit;

// Drives the top-left HUD strip (flame, wave, Start wave button, Auto waves) and the wave banner from the
// Eternal Lamp and the WaveSpawner. The look lives in the GameHUD prefab (nested in GameplayCore); edit it there.
[DefaultExecutionOrder(10)] // after EternalLamp.Start has set the flame
public class GameHud : MonoBehaviour
{
    // State colours
    static readonly Color Light = Hex("E8D6AE");
    static readonly Color Ember = Hex("E0794F");
    static readonly Color Gold = Hex("F2B655");
    static readonly Color Blood = Hex("C2412D");            // low flame, hit flash
    static readonly Color HitTint = Hex("5E0F2A");          // screen-edge vignette on a hit
    // Start wave button: solid brass when it's your move
    static readonly Color ButtonFill = Hex("C9974E");
    static readonly Color ButtonHover = Hex("DDAA5C");
    static readonly Color ButtonInk = Hex("1A130D");

    const float RowHeight = 44f;      // number + bar
    const float FoldedHeight = 32f;   // number only
    const float LowFlame = 0.25f;     // fraction of max where the flame starts to gutter
    const string DimHex = "#9A8A70";

    static bool folded; // kept across restarts in this session

    [Header("Strip")]
    [SerializeField] private LayoutElement flameBlock;
    [SerializeField] private LayoutElement waveBlock;
    [SerializeField] private UiPointer foldTab;
    [SerializeField] private Image foldChevron;

    [Header("Flame")]
    [SerializeField] private RectTransform flameIcon;
    [SerializeField] private Image flameIconImage;
    [SerializeField] private TextMeshProUGUI flameValue;
    [SerializeField] private RectTransform flameBar;
    [SerializeField] private RectTransform flameGhost;
    [SerializeField] private RectTransform flameFill;
    [SerializeField] private Image flameFillImage;
    [SerializeField] private TextMeshProUGUI lostTemplate;   // "−N" that drips down on a hit (inactive template)
    [SerializeField] private Image vignette;

    [Header("Wave")]
    [SerializeField] private TextMeshProUGUI waveValue;
    [SerializeField] private RectTransform waveBar;
    [SerializeField] private RectTransform waveFill;

    [Header("Start wave button")]
    [SerializeField] private UiPointer callButton;
    [SerializeField] private RectTransform callContent;
    [SerializeField] private Image callBg;
    [SerializeField] private Image callEdge;
    [SerializeField] private TextMeshProUGUI callLabel;
    [SerializeField] private TextMeshProUGUI callSub;
    [SerializeField] private RectTransform callKey;
    [SerializeField] private RectTransform callTimer;

    [Header("Auto waves")]
    [SerializeField] private UiPointer autoButton;
    [SerializeField] private Image autoBg;
    [SerializeField] private Image autoEdge;
    [SerializeField] private Image autoLamp;
    [SerializeField] private TextMeshProUGUI autoState;

    [Header("Banner")]
    [SerializeField] private CanvasGroup banner;
    [SerializeField] private RectTransform bannerContent;
    [SerializeField] private TextMeshProUGUI bannerTitle;
    [SerializeField] private TextMeshProUGUI bannerSub;

    const float BannerIn = 0.35f, BannerHold = 1.6f, BannerOut = 0.6f;

    private EternalLamp lamp;
    private WaveSpawner spawner;

    private float shownFlame, ghostFlame, ghostHold, shownNumber, hit, shake;
    private int printedNumber = -1;
    private Vector2 flameValueHome;
    private readonly List<Floater> floaters = new List<Floater>();

    private float shownProgress;
    private int printedRemaining = -1;
    private bool callHovered, autoHovered;
    private float callShake;
    private int printedCountdown = -1;

    private float bannerAge = -1f;

    // ---------- Setup ----------

    void Start()
    {
        lamp = FindFirstObjectByType<EternalLamp>();
        spawner = FindFirstObjectByType<WaveSpawner>();
        if (lamp == null || spawner == null || spawner.Settings == null)
        {
            gameObject.SetActive(false); // sandbox / no map settings: the prefab's plain flame text stays
            return;
        }

        foldTab.onClick = () => { folded = !folded; ApplyFold(); };
        callButton.onEnter = () => { callHovered = true; PaintCall(); };
        callButton.onExit = () => { callHovered = false; PaintCall(); };
        callButton.onClick = CallWave;
        autoButton.onEnter = () => { autoHovered = true; PaintAuto(); };
        autoButton.onExit = () => { autoHovered = false; PaintAuto(); };
        autoButton.onClick = ToggleAuto;

        flameValueHome = flameValue.rectTransform.anchoredPosition;
        lostTemplate.gameObject.SetActive(false);
        vignette.color = Color.clear;
        banner.alpha = 0f;
        banner.gameObject.SetActive(false);

        shownFlame = ghostFlame = (float)lamp.Flame / lamp.MaxFlame;
        shownNumber = lamp.Flame;

        lamp.Damaged += OnDamaged;
        spawner.WaveStarted += OnWaveStarted;
        spawner.WaveEnded += OnWaveEnded;
        spawner.AllWavesCleared += RefreshWave;
        RefreshWave();
        ApplyFold();
    }

    void OnDestroy()
    {
        if (lamp != null) lamp.Damaged -= OnDamaged;
        if (spawner == null) return;
        spawner.WaveStarted -= OnWaveStarted;
        spawner.WaveEnded -= OnWaveEnded;
        spawner.AllWavesCleared -= RefreshWave;
    }

    static void SetFill(RectTransform fill, float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        fill.anchorMax = new Vector2(fraction, 1f);
        bool show = fraction > 0.005f;
        if (fill.gameObject.activeSelf != show) fill.gameObject.SetActive(show);
    }

    // Folded: just the flame and wave numbers
    void ApplyFold()
    {
        float height = folded ? FoldedHeight : RowHeight;
        flameBlock.minHeight = flameBlock.preferredHeight = height;
        waveBlock.minHeight = waveBlock.preferredHeight = height;
        flameBar.gameObject.SetActive(!folded);
        waveBar.gameObject.SetActive(!folded);
        RefreshWave();
        SetFolded(foldChevron, folded);
    }

    // ---------- Events ----------

    void OnDamaged(int lost)
    {
        hit = 1f;
        shake = 1f;
        ghostHold = 0.45f; // let the lost chunk linger before it drains

        // "−N" drips down from the number
        var text = Instantiate(lostTemplate, lostTemplate.transform.parent);
        text.gameObject.SetActive(true);
        text.text = $"−{lost}";
        float x = flameValueHome.x + flameValue.preferredWidth + 8f + floaters.Count * 8f;
        text.rectTransform.anchoredPosition = new Vector2(x, lostTemplate.rectTransform.anchoredPosition.y);
        floaters.Add(new Floater { text = text, x = x, y = lostTemplate.rectTransform.anchoredPosition.y });
    }

    void OnWaveStarted(int wave)
    {
        printedRemaining = -1;
        RefreshWave();
        int count = spawner.EnemyCount(wave);
        string enemies = count == 1 ? "1 enemy approaches" : $"{count} enemies approach";
        if (wave == spawner.TotalWaves) ShowBanner("FINAL WAVE", enemies, Ember);
        else ShowBanner($"WAVE {wave}", enemies, Light);
    }

    void OnWaveEnded(int wave, int bonus)
    {
        RefreshWave();
        if (wave >= spawner.TotalWaves) return; // the end screen takes over
        ShowBanner($"WAVE {wave} CLEARED", bonus > 0 ? $"+{bonus} wax" : "", Gold);
    }

    void ShowBanner(string title, string subtitle, Color color)
    {
        bannerTitle.text = title;
        bannerTitle.color = color;
        bannerSub.text = subtitle;
        bannerAge = 0f;
        banner.gameObject.SetActive(true);
    }

    public void ToggleAuto()
    {
        if (PauseMenu.IsPaused || EternalLamp.IsGameOver) return;
        spawner.AutoContinue = !spawner.AutoContinue;
        RefreshWave();
    }

    public void CallWave()
    {
        if (PauseMenu.IsPaused || EternalLamp.IsGameOver) return;
        if (spawner.WaveRunning)
        {
            callShake = 1f; // already running: nudge the button instead
            return;
        }
        spawner.StartNextWave();
    }

    // Wave number and button for the current wave state
    void RefreshWave()
    {
        int total = spawner.TotalWaves;
        int wave = spawner.CurrentWave;
        bool running = spawner.WaveRunning;
        bool done = !running && wave >= total;

        // Between waves the number shows the one coming up, matching the button
        int shown = running || done ? wave : wave + 1;
        waveValue.text = $"<size=17><color={DimHex}>Wave</color></size> {shown}<size=17><color={DimHex}> / {total}</color></size>";

        if (running)
        {
            printedRemaining = -1;
            UpdateRemaining();
        }
        else if (!done)
        {
            int next = wave + 1;
            int count = spawner.EnemyCount(next);
            int bonus = spawner.BonusWax(next);
            string sub = (count == 1 ? "1 enemy" : $"{count} enemies") + (bonus > 0 ? $" · +{bonus} wax" : "");
            // Auto waves counting down: the button still starts it right away
            float countdown = spawner.AutoCountdown;
            printedCountdown = countdown >= 0f ? Mathf.CeilToInt(countdown) : -1;
            SetCallText(printedCountdown >= 0 ? $"Wave {next} in {printedCountdown}" : $"Start wave {next}", sub);
        }

        callKey.gameObject.SetActive(!running);
        callTimer.parent.gameObject.SetActive(!running && printedCountdown >= 0);
        callButton.gameObject.SetActive(!done && !folded);
        autoButton.gameObject.SetActive(!done && !folded);
        PaintCall();
        PaintAuto();
    }

    void PaintAuto()
    {
        bool on = spawner.AutoContinue;
        autoBg.color = autoHovered ? SlotHover : SlotFill;
        autoEdge.color = on || autoHovered ? Brass : SlotEdge;
        autoLamp.color = on ? BrassBright : SlotEdge;
        autoState.text = on ? "on" : "off";
        autoState.color = on ? BrassBright : TextDim;
    }

    void UpdateRemaining()
    {
        int left = spawner.EnemiesRemaining;
        if (left == printedRemaining) return;
        printedRemaining = left;
        SetCallText(left == 1 ? "1 enemy left" : $"{left} of {spawner.WaveEnemyCount} enemies left", "");
    }

    // The label sits centred when there's no subtitle (the prefab's layout group does the placing)
    void SetCallText(string label, string sub)
    {
        callLabel.text = label;
        callSub.text = sub;
        callSub.gameObject.SetActive(sub.Length > 0);
    }

    void PaintCall()
    {
        bool ready = !spawner.WaveRunning;
        callBg.color = !ready ? SlotFill : callHovered ? ButtonHover : ButtonFill;
        callEdge.color = ready ? BrassBright : SlotEdge;
        callLabel.color = ready ? ButtonInk : TextDim;
    }

    // ---------- Runtime ----------

    void Update()
    {
        float dt = PauseMenu.IsPaused ? 0f : Time.unscaledDeltaTime;
        float time = Time.unscaledTime;

        UpdateFlame(dt, time);
        UpdateWave(dt);
        UpdateBanner(dt);

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) CallWave();
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame) ToggleAuto();
    }

    void UpdateFlame(float dt, float time)
    {
        float target = (float)lamp.Flame / lamp.MaxFlame;
        shownFlame = Approach(shownFlame, target, 12f, dt);
        if (ghostHold > 0f) ghostHold -= dt;
        else ghostFlame = Approach(ghostFlame, shownFlame, 3f, dt);
        ghostFlame = Mathf.Max(ghostFlame, shownFlame);
        SetFill(flameFill, shownFlame);
        SetFill(flameGhost, ghostFlame);

        hit = Mathf.Max(0f, hit - dt * 2.2f);
        shake = Mathf.Max(0f, shake - dt * 3f);
        bool low = target <= LowFlame && lamp.Flame > 0;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 6f);

        // Gold when healthy, guttering red when low; white-hot for a moment on a hit
        Color tone = Color.Lerp(Blood, Gold, Mathf.InverseLerp(0.15f, 0.6f, shownFlame));
        flameFillImage.color = Color.Lerp(tone, Color.white, hit * 0.6f);
        flameIconImage.color = tone;

        // Count down toward the real value instead of jumping
        shownNumber = Mathf.Abs(lamp.Flame - shownNumber) < 0.5f ? lamp.Flame
            : Mathf.Lerp(shownNumber, lamp.Flame, 1f - Mathf.Exp(-14f * dt));
        int number = Mathf.RoundToInt(shownNumber);
        if (number != printedNumber)
        {
            printedNumber = number;
            flameValue.text = $"{number}<size=17><color={DimHex}> / {lamp.MaxFlame}</color></size>";
        }
        Color valueColor = low ? Color.Lerp(TextLight, Ember, pulse * 0.8f) : TextLight;
        flameValue.color = Color.Lerp(valueColor, Blood, hit);
        flameValue.rectTransform.anchoredPosition = flameValueHome + new Vector2(Shake(shake), 0f);

        // Flicker, stronger when low; a hit makes it flare
        float flick = Mathf.PerlinNoise(time * 7f, 0.3f) - 0.5f;
        float sy = 1f + flick * (low ? 0.3f : 0.12f) + hit * 0.35f;
        float sx = 1f - flick * 0.06f + hit * 0.15f;
        if (lamp.Flame == 0) sx = sy = 0.6f;
        flameIcon.localScale = new Vector3(sx, sy, 1f);

        float edge = hit * 0.6f + (low ? 0.08f + 0.08f * pulse : 0f);
        vignette.color = new Color(HitTint.r, HitTint.g, HitTint.b, edge);

        // "−N" numbers drip down and fade
        for (int i = floaters.Count - 1; i >= 0; i--)
        {
            var f = floaters[i];
            f.age += dt;
            f.text.rectTransform.anchoredPosition = new Vector2(f.x, f.y - 36f * Mathf.Sqrt(f.age));
            var c = f.text.color;
            c.a = 1f - Mathf.InverseLerp(0.4f, 1.1f, f.age);
            f.text.color = c;
            if (f.age >= 1.1f)
            {
                Destroy(f.text.gameObject);
                floaters.RemoveAt(i);
            }
        }
    }

    void UpdateWave(float dt)
    {
        int total = spawner.TotalWaves;
        int wave = spawner.CurrentWave;
        bool running = spawner.WaveRunning;

        // How far through the night: finished waves plus the share of the current one dealt with
        float progress = wave;
        if (running)
        {
            float cleared = spawner.WaveEnemyCount > 0 ? 1f - (float)spawner.EnemiesRemaining / spawner.WaveEnemyCount : 0f;
            progress = wave - 1 + cleared;
            UpdateRemaining();
        }
        else if (wave < total)
        {
            float countdown = spawner.AutoCountdown;
            int seconds = countdown >= 0f ? Mathf.CeilToInt(countdown) : -1;
            if (seconds != printedCountdown) RefreshWave();
            if (countdown >= 0f) SetFill(callTimer, countdown / spawner.AutoDelay);
        }
        shownProgress = Approach(shownProgress, progress / total, 6f, dt);
        SetFill(waveFill, shownProgress);

        callShake = Mathf.Max(0f, callShake - dt * 3f);
        callContent.anchoredPosition = new Vector2(Shake(callShake), 0f);
    }

    void UpdateBanner(float dt)
    {
        if (bannerAge < 0f) return;
        bannerAge += dt;

        float alpha;
        if (bannerAge < BannerIn) alpha = Mathf.SmoothStep(0f, 1f, bannerAge / BannerIn);
        else if (bannerAge < BannerIn + BannerHold) alpha = 1f;
        else alpha = 1f - Mathf.SmoothStep(0f, 1f, (bannerAge - BannerIn - BannerHold) / BannerOut);
        banner.alpha = alpha;

        // Letters settle inward and the title eases down from a slightly larger size
        float settle = 1f - Mathf.Pow(1f - Mathf.Clamp01(bannerAge / (BannerIn * 2f)), 3f);
        bannerTitle.characterSpacing = Mathf.Lerp(40f, 10f, settle);
        bannerContent.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, settle);

        if (bannerAge >= BannerIn + BannerHold + BannerOut)
        {
            bannerAge = -1f;
            banner.gameObject.SetActive(false);
        }
    }

    static float Approach(float current, float target, float speed, float dt)
    {
        if (Mathf.Abs(target - current) < 0.002f) return target;
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
    }

    class Floater
    {
        public TextMeshProUGUI text;
        public float x, y, age;
    }
}
