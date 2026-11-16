using UnityEngine;
using UnityEngine.Rendering.Universal;

public class MoonClouds : MonoBehaviour
{
    const int Tile = 256;           // texels per cloud tile
    const float TileWorld = 24f;    // world units per tile (larger than the visible map)

    private Light2D moon;
    private Vector2 wind;
    private Vector2 offset;
    private float baseIntensity;
    private float seed;

    public static MoonClouds Create(Transform parent, int[] layers, Color color, float intensity, Vector2 wind, float coverage)
    {
        var go = new GameObject("MoonClouds");
        go.SetActive(false); // configure before the light registers
        go.transform.SetParent(parent, false);

        var clouds = go.AddComponent<MoonClouds>();
        clouds.wind = wind;
        clouds.baseIntensity = intensity;
        clouds.seed = Random.value * 100f;

        var tex = CloudTexture(coverage);
        // The texture holds 2x2 tiles, so the light can slide a whole tile and wrap without a seam
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), Tile / TileWorld);
        sprite.name = "MoonCloudsCookie";

        clouds.moon = go.AddComponent<Light2D>();
        clouds.moon.lightType = Light2D.LightType.Sprite;
        clouds.moon.lightCookieSprite = sprite;
        clouds.moon.targetSortingLayers = layers;
        clouds.moon.color = color;
        clouds.moon.intensity = intensity;
        clouds.moon.shadowsEnabled = false;

        go.SetActive(true);
        return clouds;
    }

    void Update()
    {
        Camera cam = Camera.main;
        Vector3 center = cam != null ? cam.transform.position : Vector3.zero;

        offset += wind * Time.deltaTime;
        offset.x = Mathf.Repeat(offset.x, TileWorld);
        offset.y = Mathf.Repeat(offset.y, TileWorld);
        // Sprite spans 2 tiles; centred within half a tile of the camera it always covers the view
        transform.position = new Vector3(center.x + offset.x - TileWorld * 0.5f, center.y + offset.y - TileWorld * 0.5f, 0f);

        // Clouds thin and thicken slowly
        float breathe = Mathf.PerlinNoise(seed, Time.time * 0.05f);
        moon.intensity = baseIntensity * Mathf.Lerp(0.75f, 1.15f, breathe);
    }

    // ---------- Tileable cloud noise ----------

    static Texture2D CloudTexture(float coverage)
    {
        var tile = new float[Tile * Tile];
        float min = float.MaxValue, max = float.MinValue;
        for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
            float u = (float)x / Tile, v = (float)y / Tile;
            // Gentle domain warp makes the shapes organic; offsets wrap because Fbm is periodic
            float wu = Noise(u, v, 2, 71) - 0.5f, wv = Noise(u, v, 2, 83) - 0.5f;
            float n = Fbm(u + wu * 0.22f, v + wv * 0.22f);
            tile[y * Tile + x] = n;
            if (n < min) min = n;
            if (n > max) max = n;
        }

        // Shape into clouds: coverage = share of the sky that's cloud (dark)
        float lo = Mathf.Lerp(0.25f, 0.6f, coverage), hi = lo + 0.55f; // wide band = feathered cloud edges
        int size = Tile * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int tx = x % Tile, ty = y % Tile;
            float n = (tile[ty * Tile + tx] - min) / (max - min);
            float gap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lo, hi, n));
            float detail = Noise((float)tx / Tile, (float)ty / Tile, 10, 97); // uneven brightness inside gaps
            float light = 0.1f + 0.9f * gap * (0.72f + 0.28f * detail);       // cloud shadow is dim, not black
            byte b = (byte)(light * 255f);
            px[y * size + x] = new Color32(b, b, b, 255);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static float Fbm(float u, float v) =>
        0.62f * Noise(u, v, 3, 11) + 0.28f * Noise(u, v, 6, 23) + 0.10f * Noise(u, v, 12, 37);

    // Periodic value noise: lattice of `period` cells that wraps at u, v = 1
    static float Noise(float u, float v, int period, int salt)
    {
        float x = u * period, y = v * period;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(x0, y0, period, salt), b = Hash(x0 + 1, y0, period, salt);
        float c = Hash(x0, y0 + 1, period, salt), d = Hash(x0 + 1, y0 + 1, period, salt);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Hash(int x, int y, int period, int salt)
    {
        x = ((x % period) + period) % period;
        y = ((y % period) + period) % period;
        uint h = (uint)(x * 374761393 + y * 668265263 + salt * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0xFFFFFF;
    }
}
