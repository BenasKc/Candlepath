using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string mainMenuScene = "MainMenu";

    public static bool IsPaused { get; private set; }

    private float speedBeforePause = 1f;

    void Start()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
    }

    void OnDestroy()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (EternalLamp.IsGameOver) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            // Esc first backs out of candle placement / the burnt-candle popup, then pauses
            if (!IsPaused && TowerBuilder.Instance != null && TowerBuilder.Instance.CancelInteraction()) return;

            if (IsPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        if (IsPaused) return;
        speedBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        IsPaused = true;
        pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        Time.timeScale = speedBeforePause;
        IsPaused = false;
        pausePanel.SetActive(false);
    }

    public void Restart()
    {
        // GameSession still holds the map + difficulty, so the same run restarts
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMainMenu()
    {
        SceneManager.LoadScene(mainMenuScene);
    }
}
