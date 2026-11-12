using UnityEngine;

// In-world range display for a candle:
//   - light reach   : faint warm wash with a soft rim (what the candle reveals)
//   - spark range   : long dashes (single-target shots)
//   - aura radius   : short dots (burns everything inside)
// Rings are drawn at their real radius so the line weight is the same for every candle.
// The dashes drift slowly so the ring reads as "active" without being loud.
public class RangePreview : MonoBehaviour
{
    static readonly Color LightTint = new Color(1f, 0.78f, 0.48f, 0.13f);
    static readonly Color RingTint = new Color(1f, 0.74f, 0.45f, 0.42f);
    static readonly Color BadLight = new Color(1f, 0.35f, 0.3f, 0.10f);
    static readonly Color BadRing = new Color(1f, 0.4f, 0.35f, 0.40f);

    const float DriftDegreesPerSecond = 5f;

    private SpriteRenderer glow, sparkRing, auraRing;
    private float spin;

    public static RangePreview Create(Transform parent, Material material)
    {
        var go = new GameObject("RangePreview");
        go.transform.SetParent(parent, false);
        var preview = go.AddComponent<RangePreview>();
        preview.glow = preview.Layer("Light", material);
        preview.sparkRing = preview.Layer("SparkRange", material);
        preview.auraRing = preview.Layer("AuraRange", material);
        preview.glow.sprite = UiSprites.Glow;
        preview.Hide();
        return preview;
    }

    SpriteRenderer Layer(string name, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        if (material != null) sr.sharedMaterial = material;
        return sr;
    }

    public void SetSorting(int layerId, int ringOrder, int glowOrder)
    {
        glow.sortingLayerID = sparkRing.sortingLayerID = auraRing.sortingLayerID = layerId;
        glow.sortingOrder = glowOrder;
        sparkRing.sortingOrder = auraRing.sortingOrder = ringOrder;
    }

    // strength scales opacity (1 = placing, lower for hovering an existing candle)
    public void Show(Vector3 center, CandleCatalog.Entry entry, bool valid, float strength = 1f)
    {
        transform.position = center;
        gameObject.SetActive(true);

        Color lightC = valid ? LightTint : BadLight;
        Color ringC = valid ? RingTint : BadRing;
        lightC.a *= strength;
        ringC.a *= strength;

        float light = entry.LightRadius;
        glow.enabled = light > 0f;
        glow.transform.localScale = Vector3.one * light;
        glow.color = lightC;

        SetRing(sparkRing, entry.SparkRange, 0.30f, 0.16f, ringC);
        SetRing(auraRing, entry.AuraRadius, 0.06f, 0.12f, ringC);
    }

    public void Hide() => gameObject.SetActive(false);

    void SetRing(SpriteRenderer ring, float radius, float dash, float gap, Color color)
    {
        ring.enabled = radius > 0f;
        if (!ring.enabled) return;
        ring.sprite = UiSprites.DashedRing(radius, dash, gap);
        ring.color = color;
    }

    void Update()
    {
        spin = (spin + DriftDegreesPerSecond * Time.unscaledDeltaTime) % 360f;
        sparkRing.transform.localRotation = Quaternion.Euler(0, 0, spin);
        auraRing.transform.localRotation = Quaternion.Euler(0, 0, -spin);
    }
}
