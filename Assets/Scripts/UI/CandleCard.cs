using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One candle in the build panel (CandleHUD prefab). CandlePanel copies the card for each catalog entry,
// fills it in with Show() and paints its state.
public class CandleCard : MonoBehaviour
{
    public UiPointer pointer;
    public RectTransform content;   // nudged by the shake, inside the layout slot
    public Image background;
    public Image edge;
    public Image accent;            // brass bar: selected
    public Image icon;
    public TextMeshProUGUI hotkey;
    public TextMeshProUGUI title;
    public TextMeshProUGUI kind;
    public TextMeshProUGUI rangeValue;
    public TextMeshProUGUI burnValue;
    public TextMeshProUGUI[] statLabels;
    public TextMeshProUGUI cost;
    public Image costDrop;

    [System.NonSerialized] public int index;
    [System.NonSerialized] public bool hovered;
    [System.NonSerialized] public float shake;

    public void Show(CandleCatalog.Entry entry, int i)
    {
        index = i;
        name = $"Card_{entry.displayName}";
        icon.sprite = entry.Icon;
        icon.enabled = icon.sprite != null;
        hotkey.transform.parent.gameObject.SetActive(i < 9);
        hotkey.text = (i + 1).ToString();
        title.text = entry.displayName;
        kind.text = entry.Kind.Replace("/", " & ").ToLowerInvariant();
        rangeValue.text = entry.Range > 0f ? entry.Range.ToString("0.0") : "–";
        burnValue.text = entry.BurnSeconds > 0f ? $"{entry.BurnSeconds:0}s" : "∞";
        cost.text = entry.cost.ToString();
    }
}
