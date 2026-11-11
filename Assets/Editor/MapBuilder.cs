using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        public string Lamp;           // last node
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
    }

    static Prop P(string sprite, int x, int y, int width = 1, float lift = 0f) =>
        new Prop { Sprite = sprite, X = x, Y = y, Width = width, Lift = lift };

    static Node N(string name, int x, int y, params string[] next) =>
        new Node { Name = name, Cell = new Vector2Int(x, y), Next = next };

    // Biblioteka (design doc sketch): one road, a diagonal fork into two parallel lanes; enemies pick a lane at
    // random. The lanes run to the right edge and meet in front of the Eternal Lamp.
    //
    //   y= 2               U1 ------------- U2
    //                     /                   |
    //   y=-1  S ------- F                     Lamp
    //                     \                   |
    //   y=-4               L1 ------------- L2
    static readonly MapLayout Library = new MapLayout
    {
        DisplayName = "Biblioteka",
        SceneName = "Map_2",
        AssetName = "Biblioteka",
        Spawns = new[] { "S" },
        Lamp = "Lamp",
        StartingWax = 500,
        Waves = 25,
        Music = "biblioteka_exploration_theme.ogg",
        FloorTile = "floor_library",
        PathTile = "path_library",
        Decor = new[]
        {
            // shelving along the top and bottom walls
            P("decor_bookshelf", -9, 4, 2), P("decor_shelf", -6, 4), P("decor_bookshelf", -3, 4, 2),
            P("decor_shelf", 1, 4), P("decor_bookshelf", 3, 4, 2), P("decor_shelf", 7, 4),
            P("decor_shelf", -9, -5), P("decor_bookshelf", -7, -5, 2), P("decor_crate", -4, -5),
            P("decor_bookshelf", 0, -5, 2), P("decor_shelf", 4, -5), P("decor_bookshelf", 7, -5, 2),
            // reading corners
            P("decor_reading_table", -8, 1), P("decor_books", -8, 1, 1, 0.32f),
            P("decor_reading_table", -8, -3), P("decor_crate", -6, -3),
            P("decor_reading_table", 1, 0), P("decor_books", 1, 0, 1, 0.32f),
            P("decor_reading_table", 6, -2), P("decor_crate", 8, 0),
        },
        Nodes = new[]
        {
            N("S", -10, -1, "F"),
            N("F", -5, -1, "U1", "L1"),
            N("U1", -2, 2, "U2"),
            N("U2", 9, 2, "Lamp"),
            N("L1", -2, -4, "L2"),
            N("L2", 9, -4, "Lamp"),
            N("Lamp", 9, -1),
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
        Lamp = "Lamp",
        StartingWax = 600,
        Waves = 30,
        Music = "koplycia_church_theme.ogg",
        FloorTile = "floor_chapel",
        PathTile = "path_chapel",
        Decor = new[]
        {
            // pews in rows facing the long corridor
            P("decor_pew", -3, 3, 2), P("decor_pew", 0, 3, 2), P("decor_pew", 2, 3, 2),
            P("decor_pew", -3, -4, 2), P("decor_pew", 0, -4, 2), P("decor_pew", 2, -4, 2),
            P("decor_pew", -9, 2, 2), P("decor_pew", -9, -3, 2), P("decor_pew", 7, 3, 2), P("decor_pew", 7, -4, 2),
            // busts in the corners, candleholders by the crossings
            P("decor_bust", -10, 4), P("decor_bust", 9, 4), P("decor_bust", -10, -5), P("decor_bust", 9, -5),
            P("decor_candleholders", -4, 0), P("decor_candleholders", 4, 0),
            P("decor_candleholders", -4, -2), P("decor_candleholders", 4, -2),
            P("decor_candleholders", 7, 0), P("decor_candleholders", -8, 0),
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
        P("decor_barrel", -10, 4), P("decor_barrel", -9, 4), P("decor_crate_small", -7, 4), P("decor_bucket", -3, 4),
        P("decor_tub", -1, 4), P("decor_barrel", 0, 4), P("decor_barrel", 1, 4), P("decor_crate", 4, 4),
        P("decor_bottles", 4, 4, 1, 0.32f), P("decor_barrel", 8, 4), P("decor_barrel", 8, 3), P("decor_crate_small", 5, 2),
        // left block above the entry road
        P("decor_barrel", -9, 1), P("decor_crate", -8, 1), P("decor_bottles", -8, 1, 1, 0.32f), P("decor_bucket", -10, 0),
        // inside the first bend and the middle
        P("decor_tub", -4, 0), P("decor_crate_small", -2, -2), P("decor_barrel", -3, -3), P("decor_basket", 0, -1),
        P("decor_crate", 4, -2), P("decor_basket", 5, -3), P("decor_bucket", 9, 1),
        // bottom wall and right corner
        P("decor_barrel", -10, -5), P("decor_barrel", -9, -5), P("decor_crate_small", -6, -5), P("decor_tub", -2, -4),
        P("decor_crate", 0, -5), P("decor_barrel", 1, -4), P("decor_barrel", 5, -5), P("decor_bucket", 9, -5),
        P("decor_barrel", 8, -3), P("decor_crate_small", 8, -4),
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
        foreach (var c in PathCells(layout))
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

        // Eternal Lamp sits at the end of the path (Rusys offsets it slightly from the last node)
        var lamp = Object.FindFirstObjectByType<EternalLamp>();
        if (lamp != null)
            lamp.transform.position = nodes[layout.Lamp].transform.position + new Vector3(-0.14f, 0.24f, 0f);
        else
            Debug.LogWarning("MapBuilder: template has no EternalLamp, place it manually in " + scenePath);

        BuildDecor(layout, grid);
        EnsureMusicPlayer();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
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
        var scene = EditorSceneManager.OpenScene(TemplateScene, OpenSceneMode.Single);
        var grid = Object.FindFirstObjectByType<Grid>();
        var pathCells = new HashSet<Vector2Int>();
        var pathMap = FindTilemap("Path");
        foreach (var c in pathMap.cellBounds.allPositionsWithin)
            if (pathMap.HasTile(c)) pathCells.Add(new Vector2Int(c.x, c.y));

        BuildDecor(RusysDecor, pathCells, grid);
        EnsureMusicPlayer();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        var rusys = AssetDatabase.LoadAssetAtPath<MapDefinition>(RusysMapPath);
        if (rusys != null) SetMusic(rusys, RusysMusic);
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

            var go = new GameObject(prop.Sprite);
            go.transform.SetParent(root.transform, false);
            Vector3 left = grid.GetCellCenterWorld(new Vector3Int(prop.X, prop.Y, 0));
            Vector3 right = grid.GetCellCenterWorld(new Vector3Int(prop.X + prop.Width - 1, prop.Y, 0));
            go.transform.position = (left + right) / 2f + new Vector3(0f, prop.Lift, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnsureDecorSprite(prop.Sprite);
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
        var preview = EnsurePreview(layout.AssetName.ToLowerInvariant() + "_background");
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

    /// <summary>Map-card image Assets/Environment/{name}.png (made by Art/make_map_previews.py), imported as a sprite.</summary>
    static Sprite EnsurePreview(string name)
    {
        string png = $"{EnvDir}{name}.png";
        var importer = AssetImporter.GetAtPath(png) as TextureImporter;
        if (importer == null) { Debug.LogWarning("MapBuilder: no preview " + png + ", run Art/make_map_previews.py"); return null; }
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
