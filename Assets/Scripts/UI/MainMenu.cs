using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject mapSelectPanel;

    private GameObject creditsPanel;
    private GameObject settingsPanel;

    void Start() => ShowMain();

    public void ShowMain()
    {
        mainPanel.SetActive(true);
        mapSelectPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void ShowMapSelect()
    {
        mainPanel.SetActive(false);
        mapSelectPanel.SetActive(true);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    public void ShowSettings()
    {
        if (settingsPanel == null)
            settingsPanel = SettingsPanel.Build(mainPanel.transform.parent, mainPanel.GetComponentInChildren<TMPro.TMP_Text>(true), ShowMain);
        mainPanel.SetActive(false);
        mapSelectPanel.SetActive(false);
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
    }

    public void ShowCredits()
    {
        if (creditsPanel == null)
            creditsPanel = CreditsPanel.Build(mainPanel.transform.parent, mainPanel.GetComponentInChildren<TMPro.TMP_Text>(true), ShowMain);
        mainPanel.SetActive(false);
        mapSelectPanel.SetActive(false);
        creditsPanel.SetActive(true);
        creditsPanel.transform.SetAsLastSibling();
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
