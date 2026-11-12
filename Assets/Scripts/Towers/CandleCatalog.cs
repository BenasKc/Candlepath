using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The candles the player can buy, in panel order. Hotkeys follow the order (1, 2, 3, ...).
// Lives at Resources/CandleCatalog so every map scene picks it up without extra wiring.
[CreateAssetMenu(menuName = "Candlepath/Candle Catalog")]
public class CandleCatalog : ScriptableObject
{
    public const string ResourcePath = "CandleCatalog";

    [System.Serializable]
    public class Entry
    {
        public string displayName;
        public GameObject prefab;
        [Min(0)] public int cost = 100;
        [Tooltip("Share of the cost charged to relight a burnt-out candle.")]
        [Range(0f, 1f)] public float rebuildFactor = 0.6f;
        [Tooltip("Share of the cost given back when a burnt-out candle is cleared away.")]
        [Range(0f, 1f)] public float salvageFactor = 0.1f;

        public int RebuildCost => RoundToFive(cost * rebuildFactor);
        public int SalvageValue => RoundToFive(cost * salvageFactor);

        public Sprite Icon
        {
            get
            {
                var sr = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
                return sr != null ? sr.sprite : null;
            }
        }

        // Attack reach of each damage type (0 when the prefab doesn't have it)
        public float AuraRadius => Get<CandleAura>() is CandleAura a ? a.Radius : 0f;
        public float SparkRange => Get<CandleSpark>() is CandleSpark s ? s.Range : 0f;
        public float Range => Mathf.Max(AuraRadius, SparkRange);

        // How far the candle's light reaches (what it reveals in the dark)
        public float LightRadius => Get<Light2D>() is Light2D l ? l.pointLightOuterRadius : 0f;

        T Get<T>() where T : Component => prefab != null ? prefab.GetComponentInChildren<T>() : null;

        // Seconds from placement until the candle is out (FlickerLight steps through its sprites)
        public float BurnSeconds
        {
            get
            {
                var f = prefab != null ? prefab.GetComponent<FlickerLight>() : null;
                if (f == null || !f.burningOut || f.sprites == null) return 0f;
                return Mathf.Max(0, f.sprites.Length - 1) * f.timeBetweenChanges;
            }
        }

        public string Kind
        {
            get
            {
                if (prefab == null) return "";
                bool aura = prefab.GetComponent<CandleAura>() != null;
                bool spark = prefab.GetComponent<CandleSpark>() != null;
                if (aura && spark) return "Aura/Spark";
                if (aura) return "Aura";
                if (spark) return "Spark";
                return "Light";
            }
        }

        static int RoundToFive(float v) => Mathf.Max(0, Mathf.RoundToInt(v / 5f) * 5);
    }

    [SerializeField] private List<Entry> candles = new List<Entry>();
    public IReadOnlyList<Entry> Candles => candles;

    public static CandleCatalog Load() => Resources.Load<CandleCatalog>(ResourcePath);
}
