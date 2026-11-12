using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the Library (Biblioteka) and Chapel (Koplyčia) maps from Assets/Maps/Map_1.unity (Rusys):
/// paints the Ground/Path tilemaps, creates the PathNode graph, places the Eternal Lamp, and creates the
/// MapDefinition + WaveSet assets, the MapCatalog entry and the build-settings entry.
/// Menu: Candlepath > Build Maps. Safe to run again: it rebuilds the generated scenes and assets in place.
/// </summary>
public static class MapBuilder
{
    const string TemplateScene = "Assets/Maps/Map_1.unity";   // Rusys: has the up-to-date HUD, lamp and managers; _MapTemplate is stale
    const string CatalogPath = "Assets/Data/Maps/MapCatalog.asset";
    const string EnvDir = "Assets/Environment/";
    const string DecorDir = "Assets/Decor/";
    const string MusicDir = "Assets/Music/";
    const string RusysMapPath = "Assets/Data/Maps/Rusys.asset";
    const string RusysMusic = "rusys_dark_cavern_ambient.ogg";
    const string LitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";   // Sprite-Lit material the candles and tilemaps use
    const string EnemyDir = "Assets/Prefabs/Enemies/";

    // Same playfield as Rusys: 20 x 10 cells, x -10..9, y -5..4. The path stays inside y -4..2 (top/bottom bars).
    const int MinX = -10, MaxX = 9, MinY = -5, MaxY = 4;

    class Node
    {
        public string Name;
        public Vector2Int Cell;
        public string[] Next = new string[0];
    }

    class MapLayout
    {
        public string DisplayName, SceneName, AssetName;
        public string[] Spawns;       // node names enemies start on
        public string[] Lamps;        // path ends (nodes with no next); the Eternal Lamp stands at the first, copies at the others
        public Node[] Nodes;
        public int StartingWax;
        public int Waves;
        public string Music;                 // file in Assets/Music
        public string FloorTile, PathTile;   // texture names in Assets/Environment (floor_library, path_library, ...)
        public Prop[] Decor;
    }

    // A static decoration: sprite in Assets/Decor, the cell of its left end, how many cells it spans along x, and a
    // vertical nudge (world units) for props that sit on top of another one. It blocks tower building on its cells.
    class Prop
    {
        public string Sprite;
        public int X, Y, Width;
        public float Lift;
        public Color Tint = Color.white;
    }

    // Lift for props standing on a decor_table / on the decor_commode altar; tall shelves on the top row are
    // nudged down so their tops stay on screen
    const float TableTop = 0.55f, AltarTop = 0.8f, WallBack = -0.3f;

    // The camera (CameraFit, 16:9) shows x -8.9..8.9, so columns -10 and 9 are off screen
    const int VisibleMinX = -9, VisibleMaxX = 8;
    const float ScreenRightX = 5f * 16f / 9f;   // right edge of the 16:9 view (orthographic size 5)

    static readonly Color Burgundy = new Color(0.62f, 0.3f, 0.26f);   // for the white leather armchair
    static readonly Color Stone = new Color(0.62f, 0.6f, 0.58f);      // takes the glare off the white marble busts

    static Prop Tinted(Prop p, Color tint)
    {
        p.Tint = tint;
        return p;
    }

    static Prop P(string sprite, int x, int y, int width = 1, float lift = 0f) =>
        new Prop { Sprite = sprite, X = x, Y = y, Width = width, Lift = lift };

    static Node N(string name, int x, int y, params string[] next) =>
        new Node { Name = name, Cell = new Vector2Int(x, y), Next = next };

    // Biblioteka (design doc sketch): one road, a diagonal fork into two parallel lanes; enemies pick a lane at
    // random. Each lane runs to the right edge (x=8, the last column on screen) and ends at a lamp: the Eternal
    // Lamp at the top one, a copy of it at the bottom one. Either end takes the same flame.
    //
    //   y= 2               U1 ------------- U2 (lamp)
    //                     /
    //   y=-1  S ------- F
    //                     \
    //   y=-4               L1 ------------- L2 (lamp)
    static readonly MapLayout Library = new MapLayout
    {
        DisplayName = "Biblioteka",
        SceneName = "Map_2",
        AssetName = "Biblioteka",
        Spawns = new[] { "S" },
        Lamps = new[] { "U2", "L2" },
        StartingWax = 500,
        Waves = 25,
        Music = "biblioteka_exploration_theme.ogg",
        FloorTile = "floor_library",
        PathTile = "path_library",
        Decor = new[]
        {
            // the top wall is shelved end to end, broken by a carved cabinet and a chest of drawers with a candlestick
            P("decor_bookshelf_tall", -9, 4, 2, WallBack), P("decor_bookshelf_tall", -7, 4, 2, WallBack),
            P("decor_cabinet", -5, 4, 2, WallBack), P("decor_bookshelf_tall", -3, 4, 2, WallBack), P("decor_bookshelf_tall", -1, 4, 2, WallBack),
            P("decor_commode", 1, 4, 2, WallBack), P("decor_candlestick", 1, 4, 1, AltarTop + WallBack),
            P("decor_book_stack", 2, 4, 1, AltarTop + WallBack),
            P("decor_bookshelf_tall", 3, 4, 2, WallBack), P("decor_bookshelf_tall", 5, 4, 2, WallBack),
            P("decor_bookshelf_tall", 7, 4, 2, WallBack),
            // low shelves along the bottom wall, so they don't hide the lane above them; armchairs in burgundy
            P("decor_bookshelf_low", -9, -5, 2), P("decor_vintage_cabinet", -7, -5), Tinted(P("decor_armchair", -6, -5), Burgundy),
            P("decor_bookshelf_low", -3, -5, 2), P("decor_book_stack", -1, -5), P("decor_bookshelf_low", 1, -5, 2),
            P("decor_commode", 4, -5, 2), P("decor_oil_lamp", 4, -5, 1, AltarTop), P("decor_bookshelf_low", 7, -5, 2),
            // between the lanes: two freestanding low shelves and a reading table
            P("decor_bookshelf_low", 0, -1, 2), P("decor_table", 3, -1), P("decor_chair", 3, 0),
            P("decor_book_stack", 3, -1, 1, TableTop), P("decor_bookshelf_low", 5, -1, 2),
            // reading corners by the entrance
            P("decor_table", -8, 2), P("decor_chair", -8, 3), P("decor_oil_lamp", -8, 2, 1, TableTop),
            Tinted(P("decor_armchair", -6, 3), Burgundy),
            P("decor_table", -8, -3), P("decor_chair", -8, -2), P("decor_chess", -8, -3, 1, TableTop),
            P("decor_book_stack", -6, -4),
        },
        Nodes = new[]
        {
            N("S", -10, -1, "F"),
            N("F", -5, -1, "U1", "L1"),
            N("U1", -2, 2, "U2"),
            N("U2", 8, 2),
            N("L1", -2, -4, "L2"),
            N("L2", 8, -4),
        },
    };

    // Koplyčia (design doc sketch): one long corridor crossed by two vertical corridors. Enemies come in from the
    // corridor ends (five entrances) and merge at the crossings, so one candle at a crossing lights four stretches.
    //
    //   y= 2           T1               T2
    //                  |                |
    //   y=-1  L ------ X1 ------------- X2 ------ Lamp
    //                  |                |
    //   y=-4           B1               B2
    static readonly MapLayout Chapel = new MapLayout
    {
        DisplayName = "Koplyčia",
        SceneName = "Map_3",
        AssetName = "Koplycia",
        Spawns = new[] { "L", "T1", "B1", "T2", "B2" },
        Lamps = new[] { "Lamp" },
        StartingWax = 600,
        Waves = 30,
        Music = "koplycia_church_theme.ogg",
        FloorTile = "floor_chapel",
        PathTile = "path_chapel",
        Decor = new[]
        {
            // pews in rows on both sides of the long corridor, with an aisle at x=0 between the corridors
            P("decor_pew", -9, 3, 2), P("decor_pew", -7, 3, 2), P("decor_pew", -4, 3, 2), P("decor_pew", -2, 3, 2),
            P("decor_pew", 1, 3, 2), P("decor_pew", 3, 3, 2), P("decor_pew", 6, 3, 2),
            P("decor_pew", -9, -4, 2), P("decor_pew", -7, -4, 2), P("decor_pew", -4, -4, 2), P("decor_pew", -2, -4, 2),
            P("decor_pew", 1, -4, 2), P("decor_pew", 3, -4, 2), P("decor_pew", 6, -4, 2),
            // busts closing the aisle at both walls
            Tinted(P("decor_bust", 0, 4), Stone), Tinted(P("decor_bust", 0, -5), Stone),
            // the altar by the Eternal Lamp: carved chest of drawers with the candleholders on it.
            // No candles on the floor: they'd read as the player's candles.
            P("decor_commode", 7, 1, 2), P("decor_candleholders", 7, 1, 2, AltarTop),
        },
        Nodes = new[]
        {
            N("L", -10, -1, "X1"),
            N("T1", -5, 2, "X1"),
            N("B1", -5, -4, "X1"),
            N("X1", -5, -1, "X2"),
            N("T2", 5, 2, "X2"),
            N("B2", 5, -4, "X2"),
            N("X2", 5, -1, "Lamp"),
            N("Lamp", 9, -1),
        },
    };

    // Rusys (cellar), Map_1. Its path: in along y=-1, up to y=2, across to x=2, down to y=-4, across to x=7, up to
    // y=-1 and out to the lamp. Barrels, crates and buckets fill the corners and the inside of the bends.
    static readonly Prop[] RusysDecor =
    {
        // top wall
        P("decor_barrel", -9, 4), P("decor_crate_small", -7, 4), P("decor_bucket", -3, 4),
        P("decor_tub", -1, 4), P("decor_barrel", 0, 4), P("decor_barrel", 1, 4), P("decor_crate", 4, 4),
        P("decor_bottles", 4, 4, 1, 0.32f), P("decor_barrel", 8, 4), P("decor_barrel", 8, 3), P("decor_crate_small", 5, 2),
        // left block above the entry road
        P("decor_barrel", -9, 1), P("decor_crate", -8, 1), P("decor_bottles", -8, 1, 1, 0.32f), P("decor_bucket", -9, 0),
        // inside the first bend and the middle
        P("decor_tub", -4, 0), P("decor_crate_small", -2, -2), P("decor_barrel", -3, -3), P("decor_basket", 0, -1),
        P("decor_crate", 4, -2), P("decor_basket", 5, -3), P("decor_bucket", 8, 1),
        // bottom wall and right corner
        P("decor_barrel", -9, -5), P("decor_crate_small", -6, -5), P("decor_tub", -2, -4),
        P("decor_crate", 0, -5), P("decor_barrel", 1, -4), P("decor_barrel", 5, -5),
        P("decor_barrel", 8, -3), P("decor_crate_small", 8, -4),
        // chests and cheese boxes
        P("decor_chest", -5, 4), P("decor_chest", 6, -5), P("decor_cheese_box", 4, -2, 1, 0.32f), P("decor_cheese_box", -7, -5),
    };

    [MenuItem("Candlepath/Build Maps")]
    public static void BuildAll()
    {
        DecorateRusys();
        foreach (var layout in new[] { Library, Chapel })
            Build(layout);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MapBuilder: Biblioteka (Map_2) and Koplyčia (Map_3) built. Review the scenes in the editor.");
    }

    static void Build(MapLayout layout)
    {
        string scenePath = $"Assets/Maps/{layout.SceneName}.unity";
        BuildScene(layout, scenePath);
        var waves = BuildWaveSet(layout);
        var map = BuildDefinition(layout, waves);
        AddToCatalog(map);
        AddToBuildSettings(scenePath);
    }

    // ---------------------------------------------------------------- scene

    // Cells whose centre lies within 0.72 of a path segment: one cell wide on straight runs, a connected
    // staircase on diagonals.
    static List<Vector2Int> PathCells(MapLayout layout)
    {
        var byName = layout.Nodes.ToDictionary(n => n.Name);
        var cells = new HashSet<Vector2Int>();
        foreach (var n in layout.Nodes)
            foreach (var nextName in n.Next)
            {
                Vector2 a = n.Cell, b = byName[nextName].Cell;
                for (int x = Mathf.Min(n.Cell.x, byName[nextName].Cell.x) - 1; x <= Mathf.Max(n.Cell.x, byName[nextName].Cell.x) + 1; x++)
                    for (int y = Mathf.Min(n.Cell.y, byName[nextName].Cell.y) - 1; y <= Mathf.Max(n.Cell.y, byName[nextName].Cell.y) + 1; y++)
                        if (DistanceToSegment(new Vector2(x, y), a, b) <= 0.72f)
                            cells.Add(new Vector2Int(x, y));
            }
        return cells.ToList();
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static void BuildScene(MapLayout layout, string scenePath)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null) AssetDatabase.DeleteAsset(scenePath);
        AssetDatabase.CopyAsset(TemplateScene, scenePath);
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var grid = Object.FindFirstObjectByType<Grid>();

        BuildPath(layout, grid);
        BuildDecor(layout, grid);
        EnsureMusicPlayer();
        EnsureNightAmbient();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RenderPreview(PreviewName(layout));
    }

    /// <summary>
    /// Repaints the path, rebuilds the PathNode graph and moves the Eternal Lamp in the existing Biblioteka and
    /// Koplyčia scenes, then re-places the props around the new path; everything else in the scenes is kept.
    /// </summary>
    [MenuItem("Candlepath/Rebuild Map Paths")]
    public static void RebuildPaths()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var layout in new[] { Library, Chapel })
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Maps/{layout.SceneName}.unity", OpenSceneMode.Single);
            var grid = Object.FindFirstObjectByType<Grid>();
            BuildPath(layout, grid);
            BuildDecor(layout, grid);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RenderPreview(PreviewName(layout));
            SetPreview(MapAssetPath(layout), PreviewName(layout));
        }
        AssetDatabase.SaveAssets();
        Debug.Log("MapBuilder: paths and lamps rebuilt in Biblioteka (Map_2) and Koplyčia (Map_3).");
    }

    // Tiles, PathNode graph and Eternal Lamp for the open scene
    static void BuildPath(MapLayout layout, Grid grid)
    {
        var ground = FindTilemap("Ground");
        var pathMap = FindTilemap("Path");
        var floorTile = EnsureTile(layout.FloorTile);
        var pathTile = EnsureTile(layout.PathTile);

        // Tilemaps (same convention as Rusys: ground covers everything, path cells carry the path tile on both maps)
        ground.ClearAllTiles();
        pathMap.ClearAllTiles();
        for (int x = MinX; x <= MaxX; x++)
            for (int y = MinY; y <= MaxY; y++)
                ground.SetTile(new Vector3Int(x, y, 0), floorTile);
        var cells = PathCells(layout);
        // ends in the last column on screen: one more (off-screen) tile, so the path runs out past the edge
        foreach (var end in layout.Lamps.Select(name => layout.Nodes.First(n => n.Name == name).Cell))
            if (end.x == VisibleMaxX) cells.Add(end + Vector2Int.right);
        foreach (var c in cells)
        {
            ground.SetTile(new Vector3Int(c.x, c.y, 0), pathTile);
            pathMap.SetTile(new Vector3Int(c.x, c.y, 0), pathTile);
        }

        // Path graph
        var pathRoot = GameObject.Find("PathRoot");
        pathRoot.transform.position = Vector3.zero;
        for (int i = pathRoot.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(pathRoot.transform.GetChild(i).gameObject);

        var nodes = new Dictionary<string, PathNode>();
        foreach (var n in layout.Nodes)
        {
            var go = new GameObject(n.Name);
            go.transform.SetParent(pathRoot.transform, false);
            go.transform.position = grid.GetCellCenterWorld(new Vector3Int(n.Cell.x, n.Cell.y, 0));
            nodes[n.Name] = go.AddComponent<PathNode>();
        }
        foreach (var n in layout.Nodes)
        {
            var so = new SerializedObject(nodes[n.Name]);
            var next = so.FindProperty("next");
            next.arraySize = n.Next.Length;
            for (int i = 0; i < n.Next.Length; i++)
                next.GetArrayElementAtIndex(i).objectReferenceValue = nodes[n.Next[i]];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var mapPath = pathRoot.GetComponent<MapPath>();
        var pathSo = new SerializedObject(mapPath);
        var spawns = pathSo.FindProperty("spawnPoints");
        spawns.arraySize = layout.Spawns.Length;
        for (int i = 0; i < layout.Spawns.Length; i++)
            spawns.GetArrayElementAtIndex(i).objectReferenceValue = nodes[layout.Spawns[i]];
        pathSo.ApplyModifiedPropertiesWithoutUndo();

        // Eternal Lamp (with its light) sits at the first path end (Rusys offsets it slightly from the last node);
        // the other ends get a copy of it without the EternalLamp logic, which the lamp dims and flashes with itself
        var lamp = Object.FindFirstObjectByType<EternalLamp>();
        if (lamp == null)
        {
            Debug.LogWarning("MapBuilder: no EternalLamp in " + layout.SceneName + ", place it manually");
            return;
        }
        Vector3 LampAt(string node)
        {
            var p = nodes[node].transform.position + new Vector3(-0.14f, 0.24f, 0f);
            // an end in the last column on screen: centre the light on the screen edge, so half of it shows
            if (layout.Nodes.First(n => n.Name == node).Cell.x == VisibleMaxX) p.x = ScreenRightX;
            return p;
        }
        lamp.transform.position = LampAt(layout.Lamps[0]);

        foreach (var old in Object.FindObjectsByType<FlickerLight>(FindObjectsSortMode.None))
            if (old.name.StartsWith(LampCopyName)) Object.DestroyImmediate(old.gameObject);
        var copies = new List<FlickerLight>();
        for (int i = 1; i < layout.Lamps.Length; i++)
        {
            var copy = Object.Instantiate(lamp.gameObject, lamp.transform.parent);
            copy.name = $"{LampCopyName} {layout.Lamps[i]}";
            Object.DestroyImmediate(copy.GetComponent<EternalLamp>());
            copy.transform.position = LampAt(layout.Lamps[i]);
            copies.Add(copy.GetComponent<FlickerLight>());
        }
        var lampSo = new SerializedObject(lamp);
        var ends = lampSo.FindProperty("otherEnds");
        ends.arraySize = copies.Count;
        for (int i = 0; i < copies.Count; i++) ends.GetArrayElementAtIndex(i).objectReferenceValue = copies[i];
        lampSo.ApplyModifiedPropertiesWithoutUndo();
        HideLampSprites();
    }

    const string LampCopyName = "eternal_lamp end";

    // The path ends show only the lamp's light, not a lamp: switch off the sprite on the lamp and its copies
    // (it used to sit on the Default sorting layer, under the floor tiles)
    static void HideLampSprites()
    {
        var lamp = Object.FindFirstObjectByType<EternalLamp>();
        if (lamp == null) return;
        foreach (var f in Object.FindObjectsByType<FlickerLight>(FindObjectsSortMode.None))
        {
            if (f.gameObject != lamp.gameObject && !f.name.StartsWith(LampCopyName)) continue;
            var sr = f.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
    }

    // ---------------------------------------------------------------- tiles and decor

    /// <summary>Imports Assets/Environment/{name}.png as a single 512 px sprite (1 cell) and returns its Tile asset.</summary>
    static TileBase EnsureTile(string name)
    {
        string png = $"{EnvDir}{name}.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(png);
        if (importer == null) throw new System.Exception("MapBuilder: missing " + png + " (run Art/Blender/generate_map_textures.py)");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 512;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        string assetPath = $"{EnvDir}{name}_0.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, assetPath);
        }
        tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static Sprite EnsureDecorSprite(string name)
    {
        string png = $"{DecorDir}{name}.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(png);
        if (importer == null) throw new System.Exception("MapBuilder: missing " + png + " (run Art/Blender/generate_decor.py)");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 107;   // same scale as the tower and enemy sprites (see README)
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(png);
    }

    /// <summary>Adds the static decorations to the existing Rusys scene (only the "Decor" object is touched).</summary>
    [MenuItem("Candlepath/Decorate Rusys")]
    public static void DecorateRusys()
    {
        Redecorate(TemplateScene, RusysDecor, RusysMapPath, "rusys_background");

        var rusys = AssetDatabase.LoadAssetAtPath<MapDefinition>(RusysMapPath);
        if (rusys != null) SetMusic(rusys, RusysMusic);
    }

    /// <summary>Swaps the props in all three map scenes for the current layouts; tiles, path, lamp and waves are left alone.</summary>
    [MenuItem("Candlepath/Redecorate Maps")]
    public static void RedecorateAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        DecorateRusys();
        foreach (var layout in new[] { Library, Chapel })
            Redecorate($"Assets/Maps/{layout.SceneName}.unity", layout.Decor, MapAssetPath(layout), PreviewName(layout));
        AssetDatabase.SaveAssets();
        Debug.Log("MapBuilder: props, night lighting and map-card previews updated for Rusys, Biblioteka and Koplyčia.");
    }

    static string MapAssetPath(MapLayout layout) => $"Assets/Data/Maps/{layout.AssetName}.asset";
    static string PreviewName(MapLayout layout) => layout.AssetName.ToLowerInvariant() + "_background";

    // Replaces only the scene's "Decor" object (path cells are read from its Path tilemap), saves the night
    // lighting into the scene and renders the map's card image
    static void Redecorate(string scenePath, Prop[] props, string mapAssetPath, string previewName)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var grid = Object.FindFirstObjectByType<Grid>();
        var pathCells = new HashSet<Vector2Int>();
        var pathMap = FindTilemap("Path");
        foreach (var c in pathMap.cellBounds.allPositionsWithin)
            if (pathMap.HasTile(c)) pathCells.Add(new Vector2Int(c.x, c.y));

        BuildDecor(props, pathCells, grid);
        HideLampSprites();
        EnsureMusicPlayer();
        EnsureNightAmbient();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RenderPreview(previewName);
        SetPreview(mapAssetPath, previewName);
    }

    // Saves the game's night ambient (NightLighting's values) in the scene as global lights, so the editor's Game
    // view looks like play mode: dark floor and path, dim props, near-black enemies. At runtime NightLighting finds
    // these lights and sets the same values; only the drifting moonlight and candlelight are added in play mode.
    static void EnsureNightAmbient()
    {
        var root = GameObject.Find("NightAmbient") ?? new GameObject("NightAmbient");
        var temp = new GameObject("NightLightingTemp");
        temp.AddComponent<NightLighting>().Apply();   // retunes existing global lights, adds the missing ones under temp
        for (int i = temp.transform.childCount - 1; i >= 0; i--)
            temp.transform.GetChild(i).SetParent(root.transform, true);
        Object.DestroyImmediate(temp);
    }

    // Map-card image: the open scene through the game camera, evenly lit so the props show
    // (darkness and candlelight are the game's job). Writes Assets/Environment/{name}.png.
    const float PreviewLight = 0.8f;

    static void RenderPreview(string name)
    {
        var cam = Camera.main;
        var globals = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None)
            .Where(l => l.lightType == Light2D.LightType.Global && l.enabled).ToList();
        foreach (var l in globals) l.enabled = false;

        var temp = new GameObject("PreviewLight");
        temp.SetActive(false);
        var light = temp.AddComponent<Light2D>();
        light.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();
        light.lightType = Light2D.LightType.Global;
        light.intensity = PreviewLight;
        temp.SetActive(true);

        const int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes($"{EnvDir}{name}.png", tex.EncodeToPNG());

        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(temp);
        foreach (var l in globals) l.enabled = true;
        AssetDatabase.ImportAsset($"{EnvDir}{name}.png");
    }

    static void SetPreview(string mapAssetPath, string previewName)
    {
        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(mapAssetPath);
        var sprite = EnsurePreview(previewName);
        if (map == null || sprite == null) return;
        var so = new SerializedObject(map);
        so.FindProperty("<Preview>k__BackingField").objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
    }

    /// <summary>Adds the object that plays MapDefinition.Music in this scene (once).</summary>
    static void EnsureMusicPlayer()
    {
        if (Object.FindFirstObjectByType<MapMusic>() != null) return;
        var go = new GameObject("MapMusic");
        go.AddComponent<AudioSource>().playOnAwake = false;
        go.AddComponent<MapMusic>();
    }

    /// <summary>Imports Assets/Music/{file} as a streamed Vorbis clip and assigns it to the map.</summary>
    static void SetMusic(MapDefinition map, string file)
    {
        string path = MusicDir + file;
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) { Debug.LogWarning("MapBuilder: missing " + path); return; }
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();

        var so = new SerializedObject(map);
        so.FindProperty("<Music>k__BackingField").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
    }

    static void BuildDecor(MapLayout layout, Grid grid) =>
        BuildDecor(layout.Decor, new HashSet<Vector2Int>(PathCells(layout)), grid);

    static void BuildDecor(Prop[] props, HashSet<Vector2Int> pathCells, Grid grid)
    {
        var old = GameObject.Find("Decor");
        if (old != null) Object.DestroyImmediate(old);

        var litMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(LitMaterialGuid));
        int candlesLayer = SortingLayer.NameToID("Candles");

        var root = new GameObject("Decor");
        var blocked = new List<Vector2Int>();
        foreach (var prop in props)
        {
            var cells = Enumerable.Range(prop.X, prop.Width).Select(x => new Vector2Int(x, prop.Y)).ToList();
            if (cells.Any(c => pathCells.Contains(c) || c.x < MinX || c.x > MaxX || c.y < MinY || c.y > MaxY))
            {
                Debug.LogWarning($"MapBuilder: {prop.Sprite} at ({prop.X},{prop.Y}) is on the path or off the map, skipped");
                continue;
            }

            if (prop.X < VisibleMinX || prop.X + prop.Width - 1 > VisibleMaxX)
                Debug.LogWarning($"MapBuilder: {prop.Sprite} at ({prop.X},{prop.Y}) is outside the camera view");

            var go = new GameObject(prop.Sprite);
            go.transform.SetParent(root.transform, false);
            Vector3 left = grid.GetCellCenterWorld(new Vector3Int(prop.X, prop.Y, 0));
            Vector3 right = grid.GetCellCenterWorld(new Vector3Int(prop.X + prop.Width - 1, prop.Y, 0));
            go.transform.position = (left + right) / 2f + new Vector3(0f, prop.Lift, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnsureDecorSprite(prop.Sprite);
            sr.color = prop.Tint;
            if (litMaterial != null) sr.sharedMaterial = litMaterial;
            sr.sortingLayerID = candlesLayer;
            sr.sortingOrder = -prop.Y * 4 + (prop.Lift > 0 ? 1 : 0);   // lower rows in front; a prop on a table above it
            blocked.AddRange(cells);
        }

        var blocker = root.AddComponent<DecorBlocker>();
        var so = new SerializedObject(blocker);
        var arr = so.FindProperty("cells");
        arr.arraySize = blocked.Count;
        for (int i = 0; i < blocked.Count; i++) arr.GetArrayElementAtIndex(i).vector2IntValue = blocked[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Tilemap FindTilemap(string objectName) =>
        Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None).First(t => t.gameObject.name == objectName);

    // ---------------------------------------------------------------- waves

    // Introduction order follows the design doc (4.7): the first waves bring one new enemy each,
    // every 5th wave is a hard one, every 10th ends with a boss.
    static readonly (string prefab, int firstWave)[] Roster =
    {
        ("Enemy_Shadow", 1),
        ("Enemy_Moth", 3),
        ("Enemy_WindSpirit", 5),
        ("Enemy_WaxGnawer", 7),
        ("Enemy_Golem", 10),
    };

    static WaveSet BuildWaveSet(MapLayout layout)
    {
        string path = $"Assets/Data/Waves/{layout.AssetName}_Waves.asset";
        var set = AssetDatabase.LoadAssetAtPath<WaveSet>(path);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<WaveSet>();
            AssetDatabase.CreateAsset(set, path);
        }

        var so = new SerializedObject(set);
        var waves = so.FindProperty("waves");
        waves.ClearArray();

        for (int w = 1; w <= layout.Waves; w++)
        {
            var groups = new List<(string prefab, int count, float interval, float delay)>();
            bool hard = w % 5 == 0;
            foreach (var (prefab, first) in Roster)
            {
                if (w < first) continue;
                int age = w - first;
                int count = prefab switch
                {
                    "Enemy_Shadow" => 4 + w,
                    "Enemy_Moth" => 2 + age,
                    "Enemy_WindSpirit" => 1 + age / 2,
                    "Enemy_WaxGnawer" => 1 + age / 3,
                    _ => 1 + age / 4,   // golem
                };
                if (hard) count = Mathf.CeilToInt(count * 1.4f);
                float interval = Mathf.Max(0.35f, 0.9f - 0.02f * w) * (prefab == "Enemy_Golem" ? 3f : 1f);
                groups.Add((prefab, count, interval, groups.Count * 2f));
            }
            if (w % 10 == 0) groups.Add(("Enemy_Boss", w / 10, 4f, 6f));

            waves.InsertArrayElementAtIndex(waves.arraySize);
            var wave = waves.GetArrayElementAtIndex(waves.arraySize - 1);
            wave.FindPropertyRelative("<BonusWax>k__BackingField").intValue = 80 + 6 * w + (hard ? 60 : 0);
            var gp = wave.FindPropertyRelative("groups");
            gp.arraySize = groups.Count;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = gp.GetArrayElementAtIndex(i);
                var enemy = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyDir}{groups[i].prefab}.prefab")?.GetComponent<Enemy>();
                if (enemy == null) Debug.LogError($"MapBuilder: {groups[i].prefab} prefab or its Enemy component is missing");
                g.FindPropertyRelative("<EnemyPrefab>k__BackingField").objectReferenceValue = enemy;
                g.FindPropertyRelative("<Count>k__BackingField").intValue = groups[i].count;
                g.FindPropertyRelative("<Interval>k__BackingField").floatValue = groups[i].interval;
                g.FindPropertyRelative("<StartDelay>k__BackingField").floatValue = groups[i].delay;
                g.FindPropertyRelative("<SpawnPoint>k__BackingField").intValue = -1;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
        return set;
    }

    // ---------------------------------------------------------------- definition, catalog, build settings

    static MapDefinition BuildDefinition(MapLayout layout, WaveSet waves)
    {
        string path = $"Assets/Data/Maps/{layout.AssetName}.asset";
        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(path);
        if (map == null)
        {
            map = ScriptableObject.CreateInstance<MapDefinition>();
            AssetDatabase.CreateAsset(map, path);
        }

        var so = new SerializedObject(map);
        so.FindProperty("<DisplayName>k__BackingField").stringValue = layout.DisplayName;
        so.FindProperty("<SceneName>k__BackingField").stringValue = layout.SceneName;
        var preview = EnsurePreview(PreviewName(layout));
        if (preview != null) so.FindProperty("<Preview>k__BackingField").objectReferenceValue = preview;
        so.ApplyModifiedPropertiesWithoutUndo();
        SetMusic(map, layout.Music);
        so = new SerializedObject(map);
        var diffs = so.FindProperty("difficulties");
        diffs.arraySize = 1;   // Easy only, like Rusys for now
        var d = diffs.GetArrayElementAtIndex(0);
        d.FindPropertyRelative("<Difficulty>k__BackingField").enumValueIndex = (int)Difficulty.Easy;
        d.FindPropertyRelative("<WaveSet>k__BackingField").objectReferenceValue = waves;
        d.FindPropertyRelative("<StartingWax>k__BackingField").intValue = layout.StartingWax;
        d.FindPropertyRelative("<StartingFlame>k__BackingField").intValue = 100;
        d.FindPropertyRelative("<EnemySpeedMultiplier>k__BackingField").floatValue = 1f;
        d.FindPropertyRelative("<EnemyHealthMultiplier>k__BackingField").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
        return map;
    }

    /// <summary>Map-card image Assets/Environment/{name}.png (made by RenderPreview), imported as a sprite.</summary>
    static Sprite EnsurePreview(string name)
    {
        string png = $"{EnvDir}{name}.png";
        var importer = AssetImporter.GetAtPath(png) as TextureImporter;
        if (importer == null) { Debug.LogWarning("MapBuilder: no preview " + png + ", run Candlepath > Redecorate Maps"); return null; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(png);
    }

    static void AddToCatalog(MapDefinition map)
    {
        // load fresh: opening a scene unloads unused assets, which would leave an earlier reference destroyed
        var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CatalogPath);
        if (catalog == null) { Debug.LogError("MapBuilder: missing " + CatalogPath); return; }
        var so = new SerializedObject(catalog);
        var maps = so.FindProperty("maps");
        for (int i = 0; i < maps.arraySize; i++)
            if (maps.GetArrayElementAtIndex(i).objectReferenceValue == map) return;
        maps.InsertArrayElementAtIndex(maps.arraySize);
        maps.GetArrayElementAtIndex(maps.arraySize - 1).objectReferenceValue = map;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    static void AddToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == scenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
