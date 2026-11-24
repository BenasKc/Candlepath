using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Player building: pick a candle (panel or number keys), see a ghost + range ring on the grid,
// click to place it for wax. Clicking a burnt-out candle opens Rebuild / Clear.
//
//   1..9         select candle (press again to deselect)
//   Left click   place / select a burnt-out candle
//   Shift+click  place and keep the candle selected
//   Right click  cancel
//   R / X        rebuild / clear the selected burnt-out candle
//   B            jump to the next burnt-out candle
public class TowerBuilder : MonoBehaviour
{
    public Grid gameGrid;
    public BuildGrid buildGrid;
    [SerializeField] private CandleCatalog catalog;

    public static TowerBuilder Instance { get; private set; }

    public CandleCatalog Catalog => catalog;
    public int SelectedIndex { get; private set; } = -1;
    public PlacedCandle SelectedBurnt { get; private set; }
    public bool IsPlacing => SelectedIndex >= 0;

    public event Action<int> SelectionChanged;              // catalog index, -1 = none
    public event Action<PlacedCandle> BurntSelectionChanged; // null = closed
    public event Action<int> SelectRejected;                 // tried to pick a candle the player can't afford
    public event Action<int> BurntCountChanged;              // number of burnt-out candles on the map

    public int BurntCount => burnt.Count;

    static readonly Color ValidTint = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color InvalidTint = new Color(1f, 0.35f, 0.3f, 0.45f);
    static readonly Color CellValid = new Color(1f, 0.85f, 0.6f, 0.10f);
    static readonly Color CellInvalid = new Color(1f, 0.3f, 0.25f, 0.14f);
    const float HoverRangeStrength = 0.7f; // range shown when hovering a placed candle
    const float RebuildDelay = 0.12f;       // new candle rises just after the old stub starts shrinking

    private readonly Dictionary<Vector3Int, PlacedCandle> placed = new Dictionary<Vector3Int, PlacedCandle>();
    private SpriteRenderer ghost, cellMarker;
    private RangePreview range;
    private BuildHints hints;
    private Smoulder highlighted;          // burnt stub currently glowing brighter
    private readonly List<PlacedCandle> burnt = new List<PlacedCandle>(); // in burn-out order
    private WaxBank bank;
    private bool pauseMenuHandlesEscape;

    public WaxBank Bank => bank;

    void Awake()
    {
        Instance = this;
        if (catalog == null) catalog = CandleCatalog.Load();
        if (catalog == null) Debug.LogError("TowerBuilder: no CandleCatalog found in Resources.");

        bank = FindFirstObjectByType<WaxBank>();
        if (bank == null) bank = gameObject.AddComponent<WaxBank>();

        pauseMenuHandlesEscape = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include) != null;
        CreatePreviewObjects();

        // Dark, but readable: moonlight on the floor, path and props
        if (FindFirstObjectByType<NightLighting>() == null) gameObject.AddComponent<NightLighting>();

        // Map ambience, candle burning and the lantern's burn on Shadows
        if (FindFirstObjectByType<CandleSoundscape>() == null) gameObject.AddComponent<CandleSoundscape>();

        // Keep the whole map visible in any window shape
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic && cam.GetComponent<CameraFit>() == null) cam.gameObject.AddComponent<CameraFit>();
    }

    void OnDisable()
    {
        // Sandbox mode switches the builder off; don't leave the preview hanging around
        Select(-1);
        SetBurntSelection(null);
        HidePreview();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void CreatePreviewObjects()
    {
        Material mat = CandleFx.Unlit;

        SpriteRenderer Make(string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (mat != null) sr.sharedMaterial = mat;
            sr.enabled = false;
            return sr;
        }

        cellMarker = Make("BuildCell", UiSprites.CellSquare);
        ghost = Make("BuildGhost", null);
        range = RangePreview.Create(transform, mat);
        hints = BuildHints.Create(transform, gameGrid, buildGrid, mat);
        SyncSorting(catalog != null && catalog.Candles.Count > 0 ? catalog.Candles[0].prefab : null);
    }

    // ---------- Public API (used by the panel) ----------

    public bool CanAfford(int index) => bank != null && index >= 0 && index < catalog.Candles.Count
                                        && bank.CanAfford(catalog.Candles[index].cost);

    public void ToggleSelect(int index) => Select(index == SelectedIndex ? -1 : index);

    public void Select(int index)
    {
        if (catalog == null) return;
        if (index >= catalog.Candles.Count) index = -1;
        if (index >= 0 && !CanAfford(index))
        {
            bank?.NotifyCannotAfford(catalog.Candles[index].cost);
            SelectRejected?.Invoke(index);
            return;
        }
        if (index >= 0) SetBurntSelection(null);
        if (index == SelectedIndex) return;

        SelectedIndex = index;
        if (index >= 0) PrepareGhost(catalog.Candles[index]);
        else HidePreview();
        SelectionChanged?.Invoke(SelectedIndex);
    }

    // Returns true when something was cancelled (lets Esc close this before opening the pause menu)
    public bool CancelInteraction()
    {
        if (!isActiveAndEnabled) return false;
        if (IsPlacing) { Select(-1); return true; }
        if (SelectedBurnt != null) { SetBurntSelection(null); return true; }
        return false;
    }

    public void RebuildSelected()
    {
        PlacedCandle candle = SelectedBurnt;
        if (candle == null) return;
        var entry = catalog.Candles[candle.CatalogIndex];
        if (!bank.TrySpend(entry.RebuildCost)) return;

        Vector3Int cell = candle.Cell;
        RemoveCandle(candle);
        SpawnCandle(candle.CatalogIndex, cell, RebuildDelay);
        SetBurntSelection(null);
    }

    public void ClearSelected()
    {
        PlacedCandle candle = SelectedBurnt;
        if (candle == null) return;
        bank.Add(catalog.Candles[candle.CatalogIndex].SalvageValue);
        RemoveCandle(candle);
        SetBurntSelection(null);
    }

    // Opens the popup on the next burnt-out candle (cycles), with a ping so it's easy to spot
    public void SelectNextBurnt()
    {
        burnt.RemoveAll(c => c == null);
        if (burnt.Count == 0) return;
        int i = SelectedBurnt != null ? burnt.IndexOf(SelectedBurnt) : -1;
        PlacedCandle next = burnt[(i + 1) % burnt.Count];
        Select(-1);
        SetBurntSelection(next);
        CandleFx.Ping(next.gameObject);
    }

    // ---------- Update ----------

    void Update()
    {
        if (catalog == null) return;
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (PauseMenu.IsPaused || EternalLamp.IsGameOver)
        {
            HidePreview();
            return;
        }

        HandleKeys(keyboard);

        // The selected burnt candle may have been destroyed elsewhere; drop the dangling reference
        if (!ReferenceEquals(SelectedBurnt, null) && SelectedBurnt == null) SetBurntSelection(null);

        Vector2 screenPos = mouse.position.ReadValue();
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(screenPos);
        world.z = 0;
        Vector3Int cell = gameGrid.WorldToCell(world);

        if (overUi) HidePreview();
        else if (IsPlacing) UpdatePlacementPreview(cell, world);
        else UpdateHover(cell);

        if (mouse.rightButton.wasPressedThisFrame) CancelInteraction();
        if (overUi || !mouse.leftButton.wasPressedThisFrame) return;

        if (IsPlacing)
        {
            if (!buildGrid.CanBuild(cell)) return;
            int index = SelectedIndex;
            if (!bank.TrySpend(catalog.Candles[index].cost)) { Select(-1); return; }
            SpawnCandle(index, cell, 0f);

            bool keep = keyboard.shiftKey.isPressed && CanAfford(index);
            if (!keep) Select(-1);
        }
        else
        {
            placed.TryGetValue(cell, out PlacedCandle candle);
            SetBurntSelection(candle != null && candle.IsBurnedOut ? candle : null);
        }
    }

    void HandleKeys(Keyboard keyboard)
    {
        for (int i = 0; i < catalog.Candles.Count && i < 9; i++)
        {
            if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                ToggleSelect(i);
        }

        if (SelectedBurnt != null)
        {
            if (GameSettings.Pressed(BindAction.Rebuild)) RebuildSelected();
            else if (GameSettings.Pressed(BindAction.Clear) || keyboard.deleteKey.wasPressedThisFrame) ClearSelected();
        }

        if (GameSettings.Pressed(BindAction.NextBurnt)) SelectNextBurnt();

        if (!pauseMenuHandlesEscape && keyboard.escapeKey.wasPressedThisFrame) CancelInteraction();
    }

    void UpdatePlacementPreview(Vector3Int cell, Vector3 cursorWorld)
    {
        hints.Show(cursorWorld, cell);
        bool ok = buildGrid.CanBuild(cell);
        Vector3 center = gameGrid.GetCellCenterWorld(cell);

        ghost.enabled = cellMarker.enabled = true;
        ghost.transform.position = center;
        ghost.color = ok ? ValidTint : InvalidTint;

        range.Show(center, catalog.Candles[SelectedIndex], ok);
        PlaceCellMarker(center, ok ? CellValid : CellInvalid);
    }

    void UpdateHover(Vector3Int cell)
    {
        ghost.enabled = false;
        hints.Hide();
        placed.TryGetValue(cell, out PlacedCandle hovered);

        // Hovering a burning candle shows its reach
        if (hovered != null && !hovered.IsBurnedOut)
            range.Show(gameGrid.GetCellCenterWorld(cell), catalog.Candles[hovered.CatalogIndex], true, HoverRangeStrength);
        else
            range.Hide();

        // Burnt candles (hovered or selected) glow brighter to show they can be clicked
        cellMarker.enabled = false;
        PlacedCandle target = SelectedBurnt;
        if (target == null && hovered != null && hovered.IsBurnedOut) target = hovered;
        SetHighlight(target != null ? target.GetComponent<Smoulder>() : null);
    }

    void SetHighlight(Smoulder smoulder)
    {
        if (highlighted == smoulder) return;
        if (highlighted != null) highlighted.Highlighted = false;
        highlighted = smoulder;
        if (highlighted != null) highlighted.Highlighted = true;
    }

    void PlaceCellMarker(Vector3 center, Color color)
    {
        cellMarker.transform.position = center;
        cellMarker.transform.localScale = new Vector3(gameGrid.cellSize.x, gameGrid.cellSize.y, 1f);
        cellMarker.color = color;
    }

    void HidePreview()
    {
        if (ghost != null) ghost.enabled = false;
        if (range != null) range.Hide();
        if (hints != null) hints.Hide();
        if (cellMarker != null) cellMarker.enabled = false;
        SetHighlight(SelectedBurnt != null ? SelectedBurnt.GetComponent<Smoulder>() : null);
    }

    void PrepareGhost(CandleCatalog.Entry entry)
    {
        var src = entry.prefab.GetComponent<SpriteRenderer>();
        ghost.sprite = src.sprite;
        ghost.transform.localScale = entry.prefab.transform.localScale;
        SyncSorting(entry.prefab);
    }

    // Preview layers live on the candles' sorting layer: light wash under everything,
    // cell marker just under candles, rings and ghost above decor (which y-sorts up to ~20)
    void SyncSorting(GameObject prefab)
    {
        var src = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
        if (src == null) return;
        int layer = src.sortingLayerID, order = src.sortingOrder;
        ghost.sortingLayerID = cellMarker.sortingLayerID = layer;
        ghost.sortingOrder = order + 40;
        cellMarker.sortingOrder = order - 1;
        range.SetSorting(layer, order + 39, order - 40);
        hints.SetSorting(layer, order - 41);
    }

    // ---------- Placement bookkeeping ----------

    void SpawnCandle(int index, Vector3Int cell, float fxDelay)
    {
        var entry = catalog.Candles[index];
        GameObject go = Instantiate(entry.prefab, gameGrid.GetCellCenterWorld(cell), Quaternion.identity);
        var candle = go.AddComponent<PlacedCandle>();
        candle.Init(index, cell);
        buildGrid.Occupy(cell, go);
        placed[cell] = candle;
        candle.BurnedOut += OnCandleBurnedOut;
        CandleFx.Build(go, fxDelay);
        Sfx.CandleLit(go.transform.position, fxDelay);
    }

    // Frees the cell right away; the old candle shrinks out and destroys itself
    void RemoveCandle(PlacedCandle candle)
    {
        candle.BurnedOut -= OnCandleBurnedOut;
        if (burnt.Remove(candle)) BurntCountChanged?.Invoke(burnt.Count);
        placed.Remove(candle.Cell);
        buildGrid.Free(candle.Cell, candle.gameObject);
        CandleFx.Remove(candle.gameObject);
    }

    void OnCandleBurnedOut(PlacedCandle candle)
    {
        burnt.Add(candle);
        BurntCountChanged?.Invoke(burnt.Count);
    }

    void SetBurntSelection(PlacedCandle candle)
    {
        if (ReferenceEquals(candle, SelectedBurnt)) return;
        SelectedBurnt = candle;
        BurntSelectionChanged?.Invoke(candle);
    }
}
