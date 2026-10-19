using UnityEngine.SceneManagement;

public static class GameSession
{
    public static MapDefinition Map { get; private set; }
    public static Difficulty Difficulty { get; private set; }
    public static bool HasSelection => Map != null;

    public static void StartMap(MapDefinition map, Difficulty difficulty)
    {
        Map = map;
        Difficulty = difficulty;
        SceneManager.LoadScene(map.SceneName);
    }
}
