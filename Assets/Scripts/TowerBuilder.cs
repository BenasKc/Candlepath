using UnityEngine;
using UnityEngine.InputSystem;

public class TowerBuilder : MonoBehaviour
{
    public GameObject prefabToPlace;
    public Grid gameGrid;

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            PlacePrefabOnGrid();
        }
    }

    void PlacePrefabOnGrid()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPos.z = 0;
        
        Vector3Int cellPosition = gameGrid.WorldToCell(mouseWorldPos);

        Vector3 snapPosition = gameGrid.GetCellCenterWorld(cellPosition);

        Instantiate(prefabToPlace, snapPosition, Quaternion.identity);
    }
}