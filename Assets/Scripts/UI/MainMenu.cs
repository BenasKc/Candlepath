using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject mapSelectPanel;

    void Start() => ShowMain();

    public void ShowMain()
    {
        mainPanel.SetActive(true);
        mapSelectPanel.SetActive(false);
    }

    public void ShowMapSelect()
    {
        mainPanel.SetActive(false);
        mapSelectPanel.SetActive(true);
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
