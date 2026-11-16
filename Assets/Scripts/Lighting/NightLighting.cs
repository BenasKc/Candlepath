using UnityEngine;
using UnityEngine.Rendering.Universal;

public class NightLighting : MonoBehaviour
{
    [SerializeField] private Color moonlight = new Color(0.62f, 0.72f, 0.85f);
    [Header("Base ambient")]
    [SerializeField, Range(0f, 1f)] private float ground = 0.06f;
    [SerializeField, Range(0f, 1f)] private float path = 0.05f;
    [SerializeField, Range(0f, 1f)] private float props = 0.06f;
    [SerializeField, Range(0f, 1f)] private float enemies = 0.02f; // bodies near-black; colour only in candlelight

    [Header("Moonlight through clouds")]
    [SerializeField, Range(0f, 1f)] private float cloudLight = 0.2f;     // extra light in the cloud gaps
    [SerializeField, Range(0f, 1f)] private float cloudCoverage = 0.5f;  // share of the map under cloud
    [SerializeField] private Vector2 wind = new Vector2(0.28f, 0.08f);   // world units per second

    void Start()
    {
        Apply();
        if (cloudLight > 0f)
        {
            var layers = new System.Collections.Generic.List<int>();
            foreach (var name in new[] { "Ground", "Path", "Candles" })
            {
                int id = SortingLayer.NameToID(name);
                if (SortingLayer.IDToName(id) == name) layers.Add(id);
            }
            MoonClouds.Create(transform, layers.ToArray(), moonlight, cloudLight, wind, cloudCoverage);
        }
    }

    public void Apply()
    {
        Set("Ground", ground);
        Set("Path", path);
        Set("Candles", props);
        Set("Enemies", enemies);
    }

    void Set(string layerName, float intensity)
    {
        int id = SortingLayer.NameToID(layerName);
        if (!SortingLayer.IsValid(id) || SortingLayer.IDToName(id) != layerName) return;

        Light2D global = FindGlobal(id);
        if (global == null)
        {
            // Configure while inactive: global lights register their layers in OnEnable
            var go = new GameObject($"Moonlight_{layerName}");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            global = go.AddComponent<Light2D>();
            // Layers first: Awake (which fills the default layers) hasn't run on the inactive object,
            // and setting lightType walks the layer list to check for duplicate global lights
            global.targetSortingLayers = new[] { id };
            global.lightType = Light2D.LightType.Global;
            global.color = moonlight;
            global.intensity = intensity;
            go.SetActive(true);
            return;
        }

        // A global light shared with other layers (rare) is left alone to avoid changing them too
        if (global.targetSortingLayers.Length == 1) global.intensity = intensity;
    }

    static Light2D FindGlobal(int layerId)
    {
        foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (light.lightType != Light2D.LightType.Global || !light.isActiveAndEnabled) continue;
            foreach (int l in light.targetSortingLayers)
                if (l == layerId) return light;
        }
        return null;
    }
}
