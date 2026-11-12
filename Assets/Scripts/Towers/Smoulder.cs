using UnityEngine;
using UnityEngine.Rendering.Universal;

// A burnt-out candle keeps a faint, slowly breathing ember glow and the odd wisp of smoke,
// so stubs can be found in the dark without breaking the mood. Added by PlacedCandle on burn-out.
public class Smoulder : MonoBehaviour
{
    static readonly Color EmberColor = new Color(1f, 0.38f, 0.14f);
    static readonly Color SmokeColor = new Color(0.55f, 0.5f, 0.58f, 0.35f);

    const float MinIntensity = 0.22f;
    const float MaxIntensity = 0.5f;
    const float BreathSeconds = 2.6f;
    const float FadeIn = 0.8f;

    const float HighlightBoost = 2.1f;    // ember brightness while hovered / selected
    const float HighlightRadius = 1.3f;

    // Set by TowerBuilder while the stub is hovered or its popup is open
    public bool Highlighted { get; set; }

    private Light2D ember;
    private float phase, age, highlight;

    void Start()
    {
        phase = Random.value * 10f;

        // Light sits at the wick, a little above the sprite's pivot
        var go = new GameObject("Ember");
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position + new Vector3(0f, 0.12f, 0f);
        ember = go.AddComponent<Light2D>();
        ember.lightType = Light2D.LightType.Point;
        ember.color = EmberColor;
        ember.pointLightInnerRadius = 0f;
        ember.pointLightOuterRadius = 0.95f;
        ember.falloffIntensity = 0.75f;
        ember.intensity = 0f;
        ember.shadowsEnabled = false;

        CreateWisps(go.transform);
    }

    void Update()
    {
        age += Time.deltaTime;
        float breath = 0.5f + 0.5f * Mathf.Sin((Time.time + phase) * Mathf.PI * 2f / BreathSeconds);
        float flicker = Mathf.PerlinNoise(phase, Time.time * 3f) * 0.08f;
        highlight = Mathf.MoveTowards(highlight, Highlighted ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        float boost = Mathf.Lerp(1f, HighlightBoost, highlight);
        ember.intensity = (Mathf.Lerp(MinIntensity, MaxIntensity, breath) + flicker) * boost * Mathf.Clamp01(age / FadeIn);
        ember.pointLightOuterRadius = Mathf.Lerp(0.95f, HighlightRadius, highlight);
    }

    void CreateWisps(Transform parent)
    {
        var sr = GetComponent<SpriteRenderer>();
        var go = new GameObject("Wisps");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startColor = SmokeColor;
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 12;

        var emission = ps.emission;
        emission.rateOverTime = 0.9f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.04f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        // Drift sideways a little as they rise
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.6f;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = CandleFx.ParticleMaterial;
        if (sr != null)
        {
            renderer.sortingLayerID = sr.sortingLayerID;
            renderer.sortingOrder = sr.sortingOrder + 30;
        }

        ps.Play();
    }
}
