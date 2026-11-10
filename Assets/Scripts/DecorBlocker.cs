using System.Collections.Generic;
using UnityEngine;

/// <summary>Marks the cells under static decorations as occupied so no candle can be built on them.</summary>
public class DecorBlocker : MonoBehaviour
{
    [SerializeField] private List<Vector2Int> cells = new List<Vector2Int>();

    void Start()
    {
        var grid = FindFirstObjectByType<BuildGrid>();
        if (grid == null) return;
        foreach (var c in cells) grid.Occupy(new Vector3Int(c.x, c.y, 0), gameObject);
    }
}
