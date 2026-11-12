using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(100)]
public class CandleExposure : MonoBehaviour
{
    const float Cap = 1.2f; // candlelight a candle sprite may take: one candle at the top of its flicker

    static readonly List<CandleExposure> all = new List<CandleExposure>();

    private SpriteRenderer sr;
    private Light2D candleLight;
    private Color baseColor;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        candleLight = GetComponentInChildren<Light2D>();
        if (sr != null) baseColor = sr.color;
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void LateUpdate()
    {
        if (sr == null) return;

        // Candlelight reaching this sprite: URP's point light with falloff intensity 0.5 fades linearly to the edge
        Vector2 here = transform.position;
        float total = 0f;
        foreach (var c in all)
        {
            Light2D l = c.candleLight;
            if (l == null || !l.isActiveAndEnabled || l.intensity <= 0f) continue;
            float r = l.pointLightOuterRadius;
            float d = Vector2.Distance(here, l.transform.position);
            if (d < r) total += l.intensity * (1f - d / r);
        }

        float k = total > Cap ? Cap / total : 1f;
        sr.color = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, sr.color.a);
    }
}
