using UnityEngine;
using UnityEngine.InputSystem;

public class TowerBuilder : MonoBehaviour
{
    public GameObject prefabToPlace;
    public Grid gameGrid;
    public BuildGrid buildGrid;

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

        Vector3 snapPosition = gameGrid.GetCellCenterWorld(cellPosition);
        GameObject candle = Instantiate(prefabToPlace, snapPosition, Quaternion.identity);
        buildGrid.Occupy(cellPosition, candle);


    }
}