using UnityEngine;
using UnityEngine.Rendering.Universal;

// Small, code-only feedback effects for building candles:
//   Build   - candle pops up with a little squash & stretch, its light blooms outward with a brief flare,
//             a ring pulses along the ground and embers spray up.
//   Rebuild - old stub puffs into smoke and shrinks away while a fresh candle builds in its place.
//   Clear   - stub shrinks away in a puff of smoke.
// All timing uses unscaled time so the effects read the same at x1/x2/x3 game speed.
public static class CandleFx
{
    static Material unlit;

    // The default lit sprite material would be black in the dark, so effects and previews are unlit
    public static Material Unlit
    {
        get
        {
            if (unlit != null) return unlit;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            unlit = new Material(shader) { hideFlags = HideFlags.DontSave };
            return unlit;
        }
    }

    static Material particles;
    public static Material ParticleMaterial
    {
        get
        {
            if (particles != null) return particles;
            particles = new Material(Unlit) { hideFlags = HideFlags.DontSave, mainTexture = UiSprites.SoftDot.texture };
            return particles;
        }
    }

    static readonly Color EmberA = new Color(1f, 0.78f, 0.38f);
    static readonly Color EmberB = new Color(1f, 0.48f, 0.16f);
    static readonly Color SmokeA = new Color(0.42f, 0.38f, 0.46f, 0.55f);
    static readonly Color SmokeB = new Color(0.25f, 0.22f, 0.3f, 0.45f);
    static readonly Color PulseColor = new Color(1f, 0.8f, 0.5f, 0.5f);

    public static void Build(GameObject candle, float delay = 0f)
    {
        var sr = candle.GetComponent<SpriteRenderer>();
        int layer = sr != null ? sr.sortingLayerID : 0;
        int order = sr != null ? sr.sortingOrder : 0;
        Vector3 pos = candle.transform.position;

        candle.AddComponent<CandlePopIn>().delay = delay;
        Pulse(pos, layer, order - 1, delay, PulseColor, 1.15f);
        Burst(pos + new Vector3(0, 0.15f, 0), embers: true, layer, order + 30, delay + 0.05f);
    }

    // Ember-coloured ripple to draw the eye to a candle (e.g. jumping to a burnt-out stub)
    public static void Ping(GameObject candle)
    {
        var sr = candle.GetComponent<SpriteRenderer>();
        Pulse(candle.transform.position, sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) - 1, 0f,
            new Color(1f, 0.45f, 0.2f, 0.75f), 1.6f);
    }

    // Puff of smoke when a candle goes out
    public static void BurnOut(GameObject candle)
    {
        var sr = candle.GetComponent<SpriteRenderer>();
        Burst(candle.transform.position + new Vector3(0, 0.2f, 0), embers: false,
            sr != null ? sr.sortingLayerID : 0, (sr != null ? sr.sortingOrder : 0) + 30, 0f);
    }

    public static void Remove(GameObject candle)
    {
        var sr = candle.GetComponent<SpriteRenderer>();
        int layer = sr != null ? sr.sortingLayerID : 0;
        int order = sr != null ? sr.sortingOrder : 0;
        Burst(candle.transform.position + new Vector3(0, 0.1f, 0), embers: false, layer, order + 30, 0f);
        candle.AddComponent<CandleShrinkOut>();
    }

    static void Pulse(Vector3 pos, int layer, int order, float delay, Color color, float maxScale)
    {
        var go = new GameObject("Fx_Pulse");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = UiSprites.PulseRing;
        sr.sharedMaterial = Unlit;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        sr.color = Color.clear;
        var pulse = go.AddComponent<FxPulse>();
        pulse.delay = delay;
        pulse.color = color;
        pulse.maxScale = maxScale;
    }

    static void Burst(Vector3 pos, bool embers, int layer, int order, float delay)
    {
        var go = new GameObject(embers ? "Fx_Embers" : "Fx_Smoke");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // settings can't change while playing

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.4f;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = 48;
        main.startDelay = delay;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.rotation = new Vector3(-90f, 0f, 0f); // cone points up the screen

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;

        if (embers)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            shape.angle = 38f;
            shape.radius = 0.16f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(EmberA, EmberB);
            main.gravityModifier = -0.08f;
            limit.limit = 0.35f;
            limit.dampen = 0.12f;
            color.color = Fade(1f, 0f);
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
        }
        else
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            shape.angle = 70f;
            shape.radius = 0.22f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            main.startColor = new ParticleSystem.MinMaxGradient(SmokeA, SmokeB);
            main.gravityModifier = -0.03f;
            limit.limit = 0.15f;
            limit.dampen = 0.08f;
            color.color = Fade(0.9f, 0f);
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = ParticleMaterial;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;

        ps.Play();
    }

    static ParticleSystem.MinMaxGradient Fade(float from, float to)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(from, 0f), new GradientAlphaKey(from, 0.35f), new GradientAlphaKey(to, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    internal static float EaseOutBack(float t)
    {
        const float c1 = 1.9f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    internal static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
}

// Scales a freshly built candle up from the ground and blooms its light outward.
// Runs in LateUpdate so it layers on top of FlickerLight's per-frame intensity.
public class CandlePopIn : MonoBehaviour
{
    public float delay;

    const float GrowTime = 0.42f;
    const float LightTime = 0.6f;
    const float FlarePeak = 1.4f;

    private Vector3 baseScale;
    private Light2D candleLight;
    private float baseRadius, baseInner;
    private float t;

    void Awake()
    {
        baseScale = transform.localScale;
        candleLight = GetComponentInChildren<Light2D>();
        if (candleLight != null)
        {
            baseRadius = candleLight.pointLightOuterRadius;
            baseInner = candleLight.pointLightInnerRadius;
        }
        Apply(0f);
    }

    void LateUpdate()
    {
        t += Time.unscaledDeltaTime;
        float local = t - delay;
        Apply(Mathf.Max(0f, local));

        if (local >= Mathf.Max(GrowTime, LightTime))
        {
            transform.localScale = baseScale;
            if (candleLight != null)
            {
                candleLight.pointLightOuterRadius = baseRadius;
                candleLight.pointLightInnerRadius = baseInner;
            }
            Destroy(this);
        }
    }

    void Apply(float time)
    {
        float k = Mathf.Clamp01(time / GrowTime);
        float s = CandleFx.EaseOutBack(k);
        float wobble = Mathf.Sin(k * Mathf.PI); // stretch tall mid-grow, settle at the end
        transform.localScale = new Vector3(baseScale.x * s * (1f - 0.1f * wobble),
                                           baseScale.y * s * (1f + 0.14f * wobble),
                                           baseScale.z);

        if (candleLight == null) return;
        float l = Mathf.Clamp01(time / LightTime);
        float bloom = Mathf.Lerp(0.25f, 1f, CandleFx.EaseOutCubic(l));
        candleLight.pointLightInnerRadius = baseInner * bloom;
        candleLight.pointLightOuterRadius = Mathf.Max(0.01f, baseRadius * bloom);
        if (time > 0f) candleLight.intensity += FlarePeak * (1f - l) * (1f - l);
    }
}

// Shrinks and fades a removed candle, then destroys it.
public class CandleShrinkOut : MonoBehaviour
{
    const float Duration = 0.3f;

    private Vector3 baseScale;
    private SpriteRenderer sr;
    private Color baseColor;
    private float t;

    void Awake()
    {
        baseScale = transform.localScale;
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        // A removed candle must not keep attacking or animating its burn stages
        foreach (var b in GetComponents<Behaviour>())
            if (b != this) b.enabled = false;
        // Child lights (e.g. a smouldering ember) go out with it; child smoke stops emitting
        foreach (var l in GetComponentsInChildren<Light2D>()) l.enabled = false;
        foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(t / Duration);
        float s = 1f - k * k;
        transform.localScale = new Vector3(baseScale.x * (1f + 0.15f * k) * s, baseScale.y * s, baseScale.z);
        if (sr != null) sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - k));
        if (k >= 1f) Destroy(gameObject);
    }
}

// Expanding, fading ground ring
public class FxPulse : MonoBehaviour
{
    public float delay;
    public Color color;
    public float maxScale = 1.15f;

    const float Duration = 0.55f;
    private float t;
    private SpriteRenderer sr;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        t += Time.unscaledDeltaTime;
        float local = t - delay;
        if (local < 0f) return;
        float k = Mathf.Clamp01(local / Duration);
        float e = CandleFx.EaseOutCubic(k);
        transform.localScale = Vector3.one * Mathf.Lerp(0.25f, maxScale, e);
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - e));
        if (k >= 1f) Destroy(gameObject);
    }
}
