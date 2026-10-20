#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class SandboxController : MonoBehaviour
{
    public const string PrefKey = "Candlepath.Sandbox";

    class Entry
    {
        public string label;
        public GameObject prefab;
    }

    static readonly (string label, string path)[] TowerPrefabs =
    {
        ("Tea candle", "Assets/Prefabs/candle_tea_1_full.prefab"),
        ("Long candle", "Assets/Prefabs/Towers/Candle_Long.prefab"),
        ("Lantern", "Assets/Prefabs/Towers/Lantern.prefab"),
        ("Candelabra", "Assets/Prefabs/Towers/Candelabra.prefab"),
        ("Spark candle", "Assets/Prefabs/Towers/Candle_Spark.prefab"),
    };

    static readonly (string label, string path)[] EnemyPrefabs =
    {
        ("Shadow", "Assets/Prefabs/Enemies/Enemy_Shadow.prefab"),
        ("Moth", "Assets/Prefabs/Enemies/Enemy_Moth.prefab"),
        ("Wind spirit", "Assets/Prefabs/Enemies/Enemy_WindSpirit.prefab"),
        ("Wax gnawer", "Assets/Prefabs/Enemies/Enemy_WaxGnawer.prefab"),
        ("Golem", "Assets/Prefabs/Enemies/Enemy_Golem.prefab"),
        ("Boss", "Assets/Prefabs/Enemies/Enemy_Boss.prefab"),
    };

    static readonly Key[] TowerKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
    static readonly Key[] EnemyKeys = { Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y };

    private readonly List<Entry> towers = new List<Entry>();
    private readonly List<Entry> enemies = new List<Entry>();
    private readonly Dictionary<Vector3Int, GameObject> placed = new Dictionary<Vector3Int, GameObject>();

    private TowerBuilder builder;
    private MapPath path;
    private SpriteRenderer ghost;
    private Rect leftPanel, rightPanel;
    private int selected = -1;
    private bool burnOut = true;
    private int spawnCount = 5;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (!EditorPrefs.GetBool(PrefKey, false)) return;
        if (FindFirstObjectByType<MapPath>() == null || FindFirstObjectByType<TowerBuilder>() == null) return;
        new GameObject("Sandbox").AddComponent<SandboxController>();
    }

    void Awake()
    {
        builder = FindFirstObjectByType<TowerBuilder>();
        path = FindFirstObjectByType<MapPath>();

        builder.enabled = false;
        var spawner = FindFirstObjectByType<WaveSpawner>();
        if (spawner != null) spawner.enabled = false;

        Load(TowerPrefabs, towers);
        Load(EnemyPrefabs, enemies);

        var go = new GameObject("SandboxGhost");
        ghost = go.AddComponent<SpriteRenderer>();
        ghost.enabled = false;
        Shader unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"); // the lit default would be black in the dark
        if (unlit != null) ghost.sharedMaterial = new Material(unlit);

        Debug.Log("Sandbox mode: 1-5 select tower, left click place, right click remove, Q W E R T Y spawn enemies, K kill all.");
    }

    void OnDestroy()
    {
        if (ghost != null) Destroy(ghost.gameObject);
    }

    static void Load((string label, string path)[] defs, List<Entry> into)
    {
        foreach (var d in defs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.path);
            if (prefab == null)
                Debug.LogWarning($"Sandbox: missing {d.path}. Run Candlepath > Wire Sprite Animations first.");
            else
                into.Add(new Entry { label = d.label, prefab = prefab });
        }
    }

    static float GuiScale => Mathf.Max(1f, Screen.height / 720f);

    bool OverPanel(Vector2 screenPos)
    {
        float s = GuiScale;
        var gui = new Vector2(screenPos.x / s, (Screen.height - screenPos.y) / s);
        return leftPanel.Contains(gui) || rightPanel.Contains(gui);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        for (int i = 0; i < towers.Count && i < TowerKeys.Length; i++)
            if (keyboard[TowerKeys[i]].wasPressedThisFrame) SelectTower(i == selected ? -1 : i);
        for (int i = 0; i < enemies.Count && i < EnemyKeys.Length; i++)
            if (keyboard[EnemyKeys[i]].wasPressedThisFrame) Spawn(i);
        if (keyboard.kKey.wasPressedThisFrame) KillAll();

        Vector2 screenPos = mouse.position.ReadValue();
        bool overUi = PauseMenu.IsPaused || OverPanel(screenPos)
            || (UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject());

        Vector3 world = Camera.main.ScreenToWorldPoint(screenPos);
        world.z = 0;
        Vector3Int cell = builder.gameGrid.WorldToCell(world);
        bool canBuild = builder.buildGrid.CanBuild(cell);

        ghost.enabled = selected >= 0 && !overUi;
        if (ghost.enabled)
        {
            ghost.transform.position = builder.gameGrid.GetCellCenterWorld(cell);
            ghost.color = canBuild ? new Color(0.5f, 1f, 0.5f, 0.6f) : new Color(1f, 0.4f, 0.4f, 0.6f);
        }

        if (overUi) return;

        if (mouse.leftButton.wasPressedThisFrame && selected >= 0 && canBuild) Place(cell);

        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (placed.ContainsKey(cell)) Remove(cell);
            else SelectTower(-1);
        }
    }

    void SelectTower(int index)
    {
        selected = index;
        if (index < 0) return;

        GameObject prefab = towers[index].prefab;
        var src = prefab.GetComponent<SpriteRenderer>();
        ghost.sprite = src.sprite;
        ghost.sortingLayerID = src.sortingLayerID;
        ghost.sortingOrder = src.sortingOrder + 1;
        ghost.transform.localScale = prefab.transform.localScale;
    }

    void Place(Vector3Int cell)
    {
        Vector3 pos = builder.gameGrid.GetCellCenterWorld(cell);
        GameObject tower = Instantiate(towers[selected].prefab, pos, Quaternion.identity);
        var flicker = tower.GetComponent<FlickerLight>();
        if (flicker != null) flicker.burningOut = burnOut; // read in Start, which runs after this
        builder.buildGrid.Occupy(cell, tower);
        placed[cell] = tower;
    }

    void Remove(Vector3Int cell)
    {
        GameObject tower = placed[cell];
        if (tower != null) Destroy(tower);
        builder.buildGrid.Free(cell, tower);
        placed.Remove(cell);
    }

    void ClearTowers()
    {
        foreach (var cell in new List<Vector3Int>(placed.Keys)) Remove(cell);
    }

    void Spawn(int index) => StartCoroutine(SpawnRoutine(index, spawnCount));

    IEnumerator SpawnRoutine(int index, int count)
    {
        for (int i = 0; i < count; i++)
        {
            PathNode start = path.GetSpawn(-1); // random entrance
            GameObject go = Instantiate(enemies[index].prefab, start.transform.position, Quaternion.identity);
            var enemy = go.GetComponent<Enemy>();
            if (enemy == null) yield break;
            enemy.Movement.Begin(start);
            yield return new WaitForSeconds(0.6f);
        }
    }

    static void KillAll()
    {
        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None)) enemy.Kil();
    }

    void OnGUI()
    {
        float s = GuiScale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float width = Screen.width / s;

        leftPanel = new Rect(10, 10, 210, 70 + towers.Count * 28 + 56);
        GUILayout.BeginArea(leftPanel, GUI.skin.box);
        GUILayout.Label("SANDBOX - Towers (1-5)");
        for (int i = 0; i < towers.Count; i++)
        {
            bool on = GUILayout.Toggle(selected == i, $"{i + 1}  {towers[i].label}", "Button");
            if (on != (selected == i)) SelectTower(on ? i : -1);
        }
        burnOut = GUILayout.Toggle(burnOut, "Candles burn out");
        if (GUILayout.Button("Clear towers")) ClearTowers();
        GUILayout.Label("Left click: place   Right click: remove");
        GUILayout.EndArea();

        rightPanel = new Rect(width - 220, 10, 210, 70 + enemies.Count * 28 + 80);
        GUILayout.BeginArea(rightPanel, GUI.skin.box);
        GUILayout.Label("Enemies (Q W E R T Y)");
        for (int i = 0; i < enemies.Count; i++)
            if (GUILayout.Button(enemies[i].label)) Spawn(i);
        spawnCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(spawnCount, 1, 20));
        GUILayout.Label($"Spawn count: {spawnCount}");
        if (GUILayout.Button("Kill all (K)")) KillAll();
        GUILayout.BeginHorizontal();
        for (int speed = 1; speed <= 3; speed++)
            if (GUILayout.Button($"x{speed}") && !PauseMenu.IsPaused) Time.timeScale = speed;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
#endif
