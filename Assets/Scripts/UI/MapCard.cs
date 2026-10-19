using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapCard : MonoBehaviour
{
    [SerializeField] private Image preview;
    [SerializeField] private TMP_Text title;
    [SerializeField] private Button easyButton;
    [SerializeField] private Button mediumButton;
    [SerializeField] private Button hardButton;

    public void Setup(MapDefinition map)
    {
        preview.sprite = map.Preview;
        title.text = map.DisplayName;
        Bind(easyButton, map, Difficulty.Easy);
        Bind(mediumButton, map, Difficulty.Medium);
        Bind(hardButton, map, Difficulty.Hard);
    }

    void Bind(Button button, MapDefinition map, Difficulty difficulty)
    {
        button.interactable = map.GetDifficulty(difficulty) != null;
        button.onClick.AddListener(() => GameSession.StartMap(map, difficulty));
    }

}
