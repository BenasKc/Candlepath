using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiKit;

// Drives the build panel (wax, candle cards, burnt-out badge) and the burnt-candle popup from the TowerBuilder
// and the WaxBank. The look lives in the CandleHUD prefab (nested in GameplayCore); edit it there.
public class CandlePanel : MonoBehaviour
{
    // State colours
    static readonly Color Ember = Hex("E0794F");
    static readonly Color Red = Hex("D0503A");                   // can't afford
    static readonly Color Faded = Hex("9A8A70", 0.55f);          // text on a card you can't afford
    static readonly Color SlotSelected = Hex("33291F");
    static readonly Color WaxTint = Hex("E8D6AE");

    const float Margin = 20f;

    static bool folded; // reset to expanded in Start

    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform panel;

    [Header("Header")]
    [SerializeField] private RectTransform waxRow;
    [SerializeField] private TextMeshProUGUI waxText;
    [SerializeField] private UiPointer burntBadge;
    [SerializeField] private TextMeshProUGUI burntCount;

    [Header("Body")]
    [SerializeField] private RectTransform body;     // everything under the wax header; folds away
    [SerializeField] private UiPointer foldTab;
    [SerializeField] private Image foldChevron;
    [SerializeField] private CandleCard cardTemplate; // copied for each candle in the catalog

    [Header("Burnt candle popup")]
    [SerializeField] private RectTransform popup;
    [SerializeField] private TextMeshProUGUI popupName;
    [SerializeField] private PopupAction rebuildAction;
    [SerializeField] private PopupAction clearAction;

    private TowerBuilder builder;
    private WaxBank bank;
    private RectTransform canvasRect;
    private float shownWax, waxFlash, waxShake;
    private Vector2 waxRowHome;
    private readonly List<CandleCard> cards = new List<CandleCard>();

    // ---------- Setup ----------

    void Start()
    {
        builder = TowerBuilder.Instance != null ? TowerBuilder.Instance : FindFirstObjectByType<TowerBuilder>();
        if (builder == null || builder.Catalog == null)
        {
            gameObject.SetActive(false);
            return;
        }
        bank = builder.Bank;
        canvasRect = (RectTransform)canvas.transform;
        waxRowHome = waxRow.anchoredPosition;

        var list = builder.Catalog.Candles;
        for (int i = 0; i < list.Count; i++)
        {
            var card = i == 0 ? cardTemplate : Instantiate(cardTemplate, cardTemplate.transform.parent);
            card.Show(list[i], i);
            int index = i;
            card.pointer.onEnter = () => { card.hovered = true; Refresh(); };
            card.pointer.onExit = () => { card.hovered = false; Refresh(); };
            card.pointer.onClick = () => builder.ToggleSelect(index);
            cards.Add(card);
        }

        foldTab.onClick = () => { folded = !folded; ApplyFold(); };
        burntBadge.onEnter = () => burntCount.color = Ember;
        burntBadge.onExit = () => burntCount.color = TextLight;
        burntBadge.onClick = () => builder.SelectNextBurnt();
        rebuildAction.pointer.onClick = () => builder.RebuildSelected();
        clearAction.pointer.onClick = () => builder.ClearSelected();

        builder.SelectionChanged += _ => Refresh();
        builder.SelectRejected += i => { if (i >= 0 && i < cards.Count) cards[i].shake = 1f; };
        builder.BurntSelectionChanged += OnBurntSelection;
        builder.BurntCountChanged += UpdateBurntBadge;
        if (bank != null)
        {
            bank.Changed += _ => Refresh();
            bank.SpendRejected += _ => { waxFlash = 1f; waxShake = 1f; };
            shownWax = bank.Wax;
        }

        folded = false; // always start expanded
        ApplyFold();
        UpdateBurntBadge(0);
        Refresh();
        OnBurntSelection(null);
    }

    void ApplyFold()
    {
        body.gameObject.SetActive(!folded);
        SetFolded(foldChevron, folded);
    }

    void UpdateBurntBadge(int count)
    {
        burntBadge.gameObject.SetActive(count > 0);
        burntCount.text = count.ToString();
    }

    // ---------- Runtime ----------

    void Update()
    {
        bool visible = builder != null && builder.isActiveAndEnabled;
        if (canvas.enabled != visible) canvas.enabled = visible;
        if (!visible) return;

        float dt = Time.unscaledDeltaTime;

        if (bank != null)
        {
            // Count toward the real total instead of jumping
            shownWax = Mathf.Abs(bank.Wax - shownWax) < 0.5f ? bank.Wax
                : Mathf.Lerp(shownWax, bank.Wax, 1f - Mathf.Exp(-14f * dt));
            waxText.text = Mathf.RoundToInt(shownWax).ToString();
        }

        waxFlash = Mathf.Max(0f, waxFlash - dt * 2.2f);
        waxShake = Mathf.Max(0f, waxShake - dt * 3f);
        waxText.color = Color.Lerp(TextLight, Red, waxFlash);
        waxRow.anchoredPosition = waxRowHome + new Vector2(Shake(waxShake), 0f);

        foreach (var c in cards)
        {
            c.shake = Mathf.Max(0f, c.shake - dt * 3f);
            c.content.anchoredPosition = new Vector2(Shake(c.shake), 0);
        }

        if (popup.gameObject.activeSelf) PositionPopup();
    }

    void Refresh()
    {
        var list = builder.Catalog.Candles;
        foreach (var c in cards)
        {
            bool selected = builder.SelectedIndex == c.index;
            bool affordable = bank == null || bank.CanAfford(list[c.index].cost) || selected;

            c.background.color = selected ? SlotSelected : c.hovered ? SlotHover : SlotFill;
            c.edge.color = selected ? BrassBright : c.hovered ? Brass : SlotEdge;
            c.accent.enabled = selected;

            c.title.color = affordable ? TextLight : Faded;
            c.kind.color = affordable ? TextDim : Faded;
            c.rangeValue.color = c.burnValue.color = affordable ? TextLight : Faded;
            foreach (var t in c.statLabels) t.color = affordable ? TextDim : Faded;
            c.cost.color = affordable ? TextLight : Red;
            c.icon.color = affordable ? Color.white : new Color(0.45f, 0.42f, 0.4f);
            c.costDrop.color = affordable ? WaxTint : Red;
        }
        RefreshPopupCosts();
    }

    void OnBurntSelection(PlacedCandle candle)
    {
        popup.gameObject.SetActive(candle != null);
        if (candle == null) return;
        popupName.text = builder.Catalog.Candles[candle.CatalogIndex].displayName;
        RefreshPopupCosts();
        PositionPopup();
    }

    void RefreshPopupCosts()
    {
        var candle = builder.SelectedBurnt;
        if (candle == null) return;
        var entry = builder.Catalog.Candles[candle.CatalogIndex];
        bool affordable = bank == null || bank.CanAfford(entry.RebuildCost);

        rebuildAction.value.text = $"−{entry.RebuildCost}";
        rebuildAction.value.color = affordable ? TextLight : Red;
        rebuildAction.SetEnabled(affordable);

        clearAction.value.text = entry.SalvageValue > 0 ? $"+{entry.SalvageValue}" : "";
        clearAction.value.color = TextLight;
    }

    void PositionPopup()
    {
        var candle = builder.SelectedBurnt;
        Camera cam = Camera.main;
        if (candle == null || cam == null) return;

        // Sit above the candle; flip below it when there's no room at the top of the screen
        Vector3 center = builder.gameGrid.GetCellCenterWorld(candle.Cell);
        Vector3 halfCell = new Vector3(0, builder.gameGrid.cellSize.y * 0.55f, 0);
        Vector2 above = ToCanvas(cam, center + halfCell) + new Vector2(0, 8);
        Vector2 below = ToCanvas(cam, center - halfCell) - new Vector2(0, 8 + popup.sizeDelta.y);

        Vector2 size = canvasRect.rect.size;
        Vector2 pos = above.y + popup.sizeDelta.y > size.y - 12 ? below : above;
        float half = popup.sizeDelta.x * 0.5f;
        float rightLimit = size.x - panel.rect.width - Margin * 2f; // never slide under the build panel
        pos.x = Mathf.Clamp(pos.x, half + 12, Mathf.Max(half + 12, rightLimit - half));
        popup.anchoredPosition = pos;
    }

    // World point -> position in canvas units measured from the bottom-left corner
    Vector2 ToCanvas(Camera cam, Vector3 world)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cam.WorldToScreenPoint(world), null, out Vector2 local);
        return local + canvasRect.rect.size * 0.5f;
    }
}
