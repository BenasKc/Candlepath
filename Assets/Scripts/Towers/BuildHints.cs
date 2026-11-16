using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// While a candle is being placed:
//   - a soft warm light follows the cursor, as if the player were holding the candle up to look
//   - free cells near the cursor get faint chalk corner marks that fade with distance,
//     so valid spots are clear up close but the rest of the map stays dark
public class BuildHints : MonoBehaviour
{
    const int Reach = 3;                  // cells around the cursor that get marks
    const float FadeStart = 1f;           // full strength inside this distance (cells)
    const float MarkAlpha = 0.13f;        // faint chalk: noticeable up close, gone at a glance
    static readonly Color MarkColor = new Color(0.86f, 0.8f, 0.68f);

    const float LightIntensity = 0.42f;
    const float LightRadius = 2.3f;
    const float LightFollow = 18f;         // how quickly the light catches up with the cursor

    private Grid grid;
    private BuildGrid buildGrid;
    private Light2D handLight;
    private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();
    private Material material;
    private int sortingLayer, sortingOrder;
    private bool showing;
    private float fade;

    public static BuildHints Create(Transform parent, Grid grid, BuildGrid buildGrid, Material material)
    {
        var go = new GameObject("BuildHints");
        go.transform.SetParent(parent, false);
        var hints = go.AddComponent<BuildHints>();
        hints.grid = grid;
        hints.buildGrid = buildGrid;
        hints.material = material;

        var lightGo = new GameObject("HandLight");
        lightGo.transform.SetParent(go.transform, false);
        hints.handLight = lightGo.AddComponent<Light2D>();
        hints.handLight.lightType = Light2D.LightType.Point;
        hints.handLight.color = new Color(1f, 0.8f, 0.55f);
        hints.handLight.pointLightInnerRadius = 0.2f;
        hints.handLight.pointLightOuterRadius = LightRadius;
        hints.handLight.falloffIntensity = 0.65f;
        hints.handLight.intensity = 0f;
        hints.handLight.shadowsEnabled = false;
        return hints;
    }

    public void SetSorting(int layerId, int order)
    {
        sortingLayer = layerId;
        sortingOrder = order;
        foreach (var m in marks) { m.sortingLayerID = layerId; m.sortingOrder = order; }
    }

    // Call every frame while placing; cursorWorld is the raw mouse position on the map
    public void Show(Vector3 cursorWorld, Vector3Int cursorCell)
    {
        if (!showing) handLight.transform.position = cursorWorld;
        showing = true;

        float k = 1f - Mathf.Exp(-LightFollow * Time.unscaledDeltaTime);
        handLight.transform.position = Vector3.Lerp(handLight.transform.position, cursorWorld, k);

        int used = 0;
        Vector2 cellSize = grid.cellSize;
        for (int dy = -Reach; dy <= Reach; dy++)
        for (int dx = -Reach; dx <= Reach; dx++)
        {
            if (dx == 0 && dy == 0) continue; // the cursor cell has its own marker
            var cell = new Vector3Int(cursorCell.x + dx, cursorCell.y + dy, 0);
            if (!buildGrid.CanBuild(cell)) continue;

            Vector3 center = grid.GetCellCenterWorld(cell);
            float dist = Vector2.Distance(new Vector2(center.x / cellSize.x, center.y / cellSize.y),
                                          new Vector2(cursorWorld.x / cellSize.x, cursorWorld.y / cellSize.y));
            float a = 1f - Mathf.Clamp01((dist - FadeStart) / (Reach + 0.5f - FadeStart));
            if (a <= 0.01f) continue;

            var mark = Mark(used++);
            mark.transform.position = center;
            mark.transform.localScale = new Vector3(cellSize.x, cellSize.y, 1f);
            mark.color = new Color(MarkColor.r, MarkColor.g, MarkColor.b, MarkAlpha * a * a * fade);
            mark.enabled = true;
        }
        for (int i = used; i < marks.Count; i++) marks[i].enabled = false;
    }

    public void Hide()
    {
        showing = false;
        foreach (var m in marks) m.enabled = false;
    }

    void Update()
    {
        // Ease the light and marks in and out instead of popping
        fade = Mathf.MoveTowards(fade, showing ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        handLight.intensity = LightIntensity * fade;
        handLight.enabled = fade > 0f;
    }

    SpriteRenderer Mark(int i)
    {
        while (marks.Count <= i)
        {
            var go = new GameObject("CellMark");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = UiSprites.CellCorners;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerID = sortingLayer;
            sr.sortingOrder = sortingOrder;
            sr.enabled = false;
            marks.Add(sr);
        }
        return marks[i];
    }
}
