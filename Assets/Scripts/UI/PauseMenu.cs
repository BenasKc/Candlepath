using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private string mainMenuScene = "MainMenu";

    public static bool IsPaused { get; private set; }

    private float speedBeforePause = 1f;
    private GameObject settingsPanel;

    // The pause prefab has no Settings button: clone Resume's right below it
    static Button FindButton(Button[] buttons, string name)
    {
        foreach (var b in buttons) if (b.name == name) return b;
        return null;
    }

    void AddSettingsButton()
    {
        var resume = FindButton(pausePanel.GetComponentsInChildren<Button>(true), "Btn_Resume");
        if (resume == null) return;
        var copy = Instantiate(resume.gameObject, resume.transform.parent);
        copy.name = "Btn_Settings";
        copy.transform.SetSiblingIndex(resume.transform.GetSiblingIndex() + 1);
        foreach (var t in copy.GetComponentsInChildren<TMP_Text>(true)) t.text = "Settings";
        var button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OpenSettings);
    }

    void OpenSettings()
    {
        if (settingsPanel == null)
        {
            var canvas = pausePanel.GetComponentInParent<Canvas>().rootCanvas;
            settingsPanel = SettingsPanel.Build(canvas.transform, pausePanel.GetComponentInChildren<TMP_Text>(true),
                () => settingsPanel.SetActive(false));
        }
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
    }

    void Start()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AddSettingsButton();
        pausePanel.SetActive(false);
    }

    void OnDestroy()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (EternalLamp.IsGameOver || SettingsPanel.IsOpen) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame || GameSettings.Pressed(BindAction.Pause))
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
