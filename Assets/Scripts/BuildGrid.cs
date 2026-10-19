using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class BuildGrid : MonoBehaviour
{
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap pathTilemap;
    
    readonly Dictionary<Vector3Int, GameObject> occupied = new Dictionary<Vector3Int, GameObject>();

    public Vector3Int WorldToCell(Vector3 world) => grid.WorldToCell(world);
    public Vector3 CellCenter(Vector3Int cell) => grid.GetCellCenterWorld(cell);

    public bool CanBuild(Vector3Int cell) =>
        groundTilemap.HasTile(cell) && !pathTilemap.HasTile(cell) && !occupied.ContainsKey(cell);

    public void Occupy(Vector3Int cell, GameObject obj) => occupied[cell] = obj;
    public void Free(Vector3Int cell, GameObject obj) => occupied.Remove(cell);
}
