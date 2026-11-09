using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class TowerBuilder : MonoBehaviour
{
    public GameObject prefabToPlace;
    public Grid gameGrid;
    public BuildGrid buildGrid;

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool left = mouse.leftButton.wasPressedThisFrame;
        bool right = mouse.rightButton.wasPressedThisFrame;
        if (!left && !right) return;
        if (PauseMenu.IsPaused || EternalLamp.IsGameOver) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        mouseWorldPos.z = 0;
        Vector3Int cellPosition = gameGrid.WorldToCell(mouseWorldPos);

        // A wax puddle on this cell takes the click first: left = clean, right = relight
        if (WaxPuddle.TryClick(buildGrid, cellPosition, left)) return;

        if (left) PlacePrefabOnGrid(cellPosition);
    }

    void PlacePrefabOnGrid(Vector3Int cellPosition)
    {
        if (!buildGrid.CanBuild(cellPosition)) return;

        Vector3 snapPosition = gameGrid.GetCellCenterWorld(cellPosition);
        GameObject candle = Instantiate(prefabToPlace, snapPosition, Quaternion.identity);
        buildGrid.Occupy(cellPosition, candle);
    }
}
