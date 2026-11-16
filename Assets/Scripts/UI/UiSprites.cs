using System.Collections.Generic;
using UnityEngine;

// Small procedurally drawn in-world sprites (range ring and glow, build cell markers, pulse, particles).
// Shapes are anti-aliased from a signed distance and cached. The HUD's shapes are PNGs in Assets/UI/Shapes.
public static class UiSprites
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    const float WorldPpu = 128f; // texels per world unit for in-world sprites (camera shows ~108 px/unit at 1080p)

    // Dashed circle drawn at its real radius (use at scale 1), so the line stays thin for any range.
    // dash/gap are world-unit arc lengths; the count is rounded so the pattern closes cleanly.
    public static Sprite DashedRing(float radius, float dash, float gap, float width = 2.4f) =>
        Get($"dash{radius:0.00}_{dash:0.00}_{gap:0.00}_{width:0.0}", () =>
        {
            int size = Mathf.CeilToInt(radius * 2f * WorldPpu) + 8;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float c = size * 0.5f, r = radius * WorldPpu, halfW = width * 0.5f;
            int count = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / (dash + gap)));
            float duty = dash / (dash + gap);
            float segPx = 2f * Mathf.PI * r / count; // arc length of one dash+gap, in texels

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float d = Len(dx, dy);
                float line = Mathf.Clamp01(halfW + 0.5f - Mathf.Abs(d - r));
                if (line <= 0f) continue;
                float t = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 1f) * count % 1f; // position within a segment
                float along = t * segPx, dashPx = duty * segPx;
                float onDash = Mathf.Clamp01(Mathf.Min(along, dashPx - along) + 0.5f); // AA dash ends
                px[y * size + x] = new Color(1f, 1f, 1f, line * onDash);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), WorldPpu);
        });

    // Light reach: a faint warm wash fading outward, with a soft rim at the edge.
    // 2 world units across, so scale = radius.
    public static Sprite Glow => Get("glow", () =>
    {
        const int size = 192;
        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Len(x + 0.5f - c, y + 0.5f - c) / (c - 1f); // 0 center .. 1 edge
            if (d >= 1f) continue;
            float wash = Mathf.Lerp(1f, 0.25f, Mathf.SmoothStep(0f, 1f, d));
            float rim = Mathf.Exp(-Mathf.Pow((1f - d) / 0.035f, 2f)) * 1.6f;
            float fadeOut = Mathf.Clamp01((1f - d) * c); // AA outer boundary
            px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((wash + rim) * 0.5f) * fadeOut);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f);
    });

    // Chalk-style corner ticks marking a free build cell: 1 world unit across
    public static Sprite CellCorners => Get("corners", () =>
    {
        const int size = 64;
        const float inset = 10f, arm = 8f, half = 0.9f;
        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Fold into one corner (bottom-left) by symmetry
            float fx = Mathf.Min(x + 0.5f, size - x - 0.5f) - inset;
            float fy = Mathf.Min(y + 0.5f, size - y - 0.5f) - inset;
            float h = Mathf.Max(Mathf.Abs(fy) - half, fx < 0 ? -fx : Mathf.Max(0f, fx - arm)); // horizontal arm
            float v = Mathf.Max(Mathf.Abs(fx) - half, fy < 0 ? -fy : Mathf.Max(0f, fy - arm)); // vertical arm
            float d = Mathf.Min(h, v);
            px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d) * 0.8f);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    });

    // Thin soft ring for the build "pulse": 2 world units across
    public static Sprite PulseRing => Get("pulse", () => Shape(128, 128, (x, y) =>
    {
        float d = Len(x - 64f, y - 64f);
        return 2.5f - Mathf.Abs(d - 60f);
    }, 64f));

    // Soft round particle
    public static Sprite SoftDot => Get("softdot", () =>
    {
        const int size = 32;
        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Len(x + 0.5f - 16f, y + 0.5f - 16f) / 16f;
            px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    });

    // Soft rounded square, 1 world unit across, for the grid cell highlight
    public static Sprite CellSquare => Get("cell", () =>
    {
        const int size = 64;
        var tex = NewTex(size, size);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = RoundedBox(x + 0.5f - size / 2f, y + 0.5f - size / 2f, size / 2f - 3f, size / 2f - 3f, 10f);
            float fill = Mathf.Clamp01(-d) > 0 ? 0.55f : 0f;
            float border = Mathf.Clamp01(1f - Mathf.Abs(d + 1f) / 1.2f);
            float a = Mathf.Max(fill * Mathf.Clamp01(-d), border);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    });

    // ---------- helpers ----------

    static Sprite Get(string key, System.Func<Sprite> make)
    {
        if (cache.TryGetValue(key, out var s) && s != null) return s;
        s = make();
        s.name = "ui_" + key;
        cache[key] = s;
        return s;
    }

    static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
    {
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear,
        hideFlags = HideFlags.DontSave,
    };

    static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

    // Signed distance to a rounded box centered at the origin (negative inside)
    static float RoundedBox(float x, float y, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
        float outside = Len(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f));
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // Filled shape from a signed "inside" distance (positive inside), anti-aliased over one pixel
    static Sprite Shape(int w, int h, System.Func<float, float, float> inside, float ppu)
    {
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(inside(x + 0.5f, y + 0.5f) + 0.5f));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
    }
}
