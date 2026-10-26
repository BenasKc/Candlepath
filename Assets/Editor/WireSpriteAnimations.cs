using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WireSpriteAnimations
{
    const string AnimRoot = "Assets/Sprites/Anim";
    const string ReferenceSprite = "Assets/Sprites/PNGs/candle_tea_1_full.png"; // import settings are copied from it
    const string TowerTemplate = "Assets/Prefabs/candle_tea_1_full.prefab";
    const string EnemyTemplate = "Assets/Prefabs/Enemies/Enemy_Shadow.prefab";
    const string TowerDir = "Assets/Prefabs/Towers";
    const string EnemyDir = "Assets/Prefabs/Enemies";

    // prefab, frame-set name per burn stage (full, mid, low)
    static readonly (string path, string[] stages)[] Towers =
    {
        (TowerTemplate, new[] { "candle_tea_1_full", "candle_tea_2_mid", "candle_tea_3_low" }),
        (TowerDir + "/Candle_Long.prefab", new[] { "candle_long_1_full", "candle_long_2_mid", "candle_long_3_low" }),
        (TowerDir + "/Lantern.prefab", new[] { "lantern_1_full", "lantern_2_mid", "lantern_3_low" }),
        (TowerDir + "/Candelabra.prefab", new[] { "candelabra_1_full", "candelabra_2_mid", "candelabra_3_low" }),
        (TowerDir + "/Candle_Spark.prefab", new[] { "candle_spark_1_full", "candle_spark_2_mid", "candle_spark_3_low" }),
    };

    // prefab, frame-set name, path speed (0 = leave as is; placeholder values from the design doc's slow/medium/fast)
    static readonly (string path, string sprite, float speed)[] Enemies =
    {
        (EnemyTemplate, "enemy_shadow", 0f),
        (EnemyDir + "/Enemy_Moth.prefab", "enemy_moth", 2.5f),
        (EnemyDir + "/Enemy_WindSpirit.prefab", "enemy_wind_spirit", 2.5f),
        (EnemyDir + "/Enemy_WaxGnawer.prefab", "enemy_wax_gnawer", 0.8f),
        (EnemyDir + "/Enemy_Golem.prefab", "enemy_golem", 0.8f),
        (EnemyDir + "/Enemy_Boss.prefab", "enemy_boss", 0.8f),
    };

    [MenuItem("Candlepath/Wire Sprite Animations")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        ConfigureImports();

        foreach (var t in Towers) WireTower(t.path, t.stages);
        foreach (var e in Enemies) WireEnemy(e.path, e.sprite, e.speed);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Wired sprite animations into {Towers.Length} tower and {Enemies.Length} enemy prefabs.");
    }

    static void ConfigureImports()
    {
        var reference = AssetImporter.GetAtPath(ReferenceSprite) as TextureImporter;
        float ppu = reference != null ? reference.spritePixelsPerUnit : 100f;
        FilterMode filter = reference != null ? reference.filterMode : FilterMode.Bilinear;

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { AnimRoot }))
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
            if (importer == null) continue;
            if (importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, ppu)) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = filter;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    static Sprite[] LoadFrames(string name)
    {
        string dir = AnimRoot + "/" + name;
        return AssetDatabase.FindAssets("t:Sprite", new[] { dir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(s => s != null)
            .ToArray();
    }

    static void EnsureClone(string path, string template)
    {
        if (File.Exists(path) || path == template) return;
        string dir = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), Path.GetFileName(dir));
        AssetDatabase.CopyAsset(template, path);
    }

    static void WireTower(string path, string[] stageNames)
    {
        Sprite[][] frames = stageNames.Select(LoadFrames).ToArray();
        if (frames.Any(f => f.Length == 0))
        {
            Debug.LogError($"No frames found under {AnimRoot} for {path}. Is the Anim folder imported?");
            return;
        }

        bool isClone = path != TowerTemplate && !File.Exists(path);
        EnsureClone(path, TowerTemplate);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (isClone)
            {
                root.name = Path.GetFileNameWithoutExtension(path);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one * 0.8f; // the tea candle's own scale is non-uniform (0.86 x 0.71)
            }

            var sr = root.GetComponent<SpriteRenderer>();
            var flicker = root.GetComponent<FlickerLight>();
            var anim = root.GetComponent<SpriteFrameAnimator>();
            if (anim == null) anim = root.AddComponent<SpriteFrameAnimator>();

            // burn stages beyond the wax-height ones (e.g. the wax puddle) stay single sprites
            var sprites = new List<Sprite>(flicker.sprites ?? new Sprite[0]);
            while (sprites.Count < frames.Length) sprites.Add(null);
            for (int i = 0; i < frames.Length; i++) sprites[i] = frames[i][0];

            var stages = new List<SpriteFrameAnimator.Stage>();
            for (int i = 0; i < sprites.Count; i++)
                stages.Add(new SpriteFrameAnimator.Stage { frames = i < frames.Length ? frames[i] : new[] { sprites[i] } });

            flicker.sprites = sprites.ToArray();
            flicker.frameAnimator = anim;
            anim.target = sr;
            anim.stages = stages.ToArray();
            sr.sprite = frames[0][0];

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void WireEnemy(string path, string spriteName, float speed)
    {
        Sprite[] frames = LoadFrames(spriteName);
        if (frames.Length == 0)
        {
            Debug.LogError($"No frames found under {AnimRoot}/{spriteName} for {path}. Is the Anim folder imported?");
            return;
        }

        bool isClone = path != EnemyTemplate && !File.Exists(path);
        EnsureClone(path, EnemyTemplate);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (isClone) root.name = Path.GetFileNameWithoutExtension(path);

            var sr = root.GetComponent<SpriteRenderer>();
            var anim = root.GetComponent<SpriteFrameAnimator>();
            if (anim == null) anim = root.AddComponent<SpriteFrameAnimator>();

            anim.target = sr;
            anim.stages = new[] { new SpriteFrameAnimator.Stage { frames = frames } };
            sr.sprite = frames[0];
            sr.color = Color.white; // the red tint was only there to tell the placeholder dot apart

            if (speed > 0f)
            {
                var so = new SerializedObject(root.GetComponent<PathFollower>());
                so.FindProperty("speed").floatValue = speed;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
