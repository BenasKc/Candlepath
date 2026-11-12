using UnityEngine;
using UnityEngine.InputSystem;

public class TowerBuilder : MonoBehaviour
{
    public Tower selectedTowerPrefab;
    public Grid gameGrid;
    public BuildGrid buildGrid;
    public WaxManager waxManager;

    void Awake()
    {
        if (gameGrid == null) gameGrid = FindFirstObjectByType<Grid>();
        if (buildGrid == null) buildGrid = FindFirstObjectByType<BuildGrid>();
        if (waxManager == null) waxManager = FindFirstObjectByType<WaxManager>();
    }
    
    public void SelectTower(Tower towerPrefab)
    {
        selectedTowerPrefab = towerPrefab;
    }
    
    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            PlacePrefabOnGrid();
        }
    }

    void PlacePrefabOnGrid()
    {
        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPos.z = 0;
        
        Vector3Int cellPosition = gameGrid.WorldToCell(mouseWorldPos);
        if (!buildGrid.CanBuild(cellPosition)) return;

        if (waxManager != null && !waxManager.TrySpend(selectedTowerPrefab.WaxCost))
        {
            Debug.Log($"Not enough wax! Need {selectedTowerPrefab.WaxCost}.");
            return;
        }
        
        Vector3 snapPosition = gameGrid.GetCellCenterWorld(cellPosition);
        Tower candle = Instantiate(selectedTowerPrefab, snapPosition, Quaternion.identity);
        buildGrid.Occupy(cellPosition, candle.gameObject);


    }
}