using UnityEngine;

public class Tutorial : MonoBehaviour
{
    public string IntroText;
    public string PlacedCandleText;
    public string BurnedOutText;

    public string[] MessageText;
    public GameObject[] Arrows;
    public TMPro.TextMeshProUGUI TMPro;

    public GameObject TutorialCanvas;
    public GameObject TextBox;
    public GameObject ClickCatcher;

    private int backlog = 0;
    private int index = 0;
    private bool candleSpawned = false;
    private bool burnedOut = false;

    void Start()
    {
        TMPro.text = IntroText;
    }

    void Update()
    {
        if (index >= MessageText.Length && !candleSpawned && GameObject.FindGameObjectWithTag("CandleTower") != null)
        {
            candleSpawned = true;
            TMPro.text = PlacedCandleText;
            Time.timeScale = 0;
            TutorialCanvas.SetActive(true);
        }

        if (candleSpawned && !burnedOut)
        {
            bool hasBurntCandle = (TowerBuilder.Instance != null && TowerBuilder.Instance.BurntCount > 0);

            if (!hasBurntCandle)
            {
                PlacedCandle candle = FindAnyObjectByType<PlacedCandle>();
                if (candle != null && candle.IsBurnedOut) hasBurntCandle = true;
            }

            if (hasBurntCandle)
            {
                burnedOut = true;
                TMPro.text = BurnedOutText;
                Time.timeScale = 0;
                TutorialCanvas.SetActive(true);
            }
        }
    }

    public void NextTutorialMessage()
    {
        if (burnedOut || (candleSpawned && TutorialCanvas.activeSelf && index >= MessageText.Length))
        {
            Time.timeScale = 1f;
            TutorialCanvas.SetActive(false);
            return;
        }

        if (index < MessageText.Length)
        {
            TMPro.text = MessageText[index];
            if (Arrows != null && index < Arrows.Length && Arrows[index] != null)
            {
                Arrows[index].SetActive(true);
            }

            if (index > 0 && Arrows != null && index < Arrows.Length && Arrows[index] != null && Arrows[index].name != "HOLD")
            {
                if (Arrows[index - 1] != null) Arrows[index - 1].SetActive(false);
                if (backlog > 0)
                {
                    for (int i = 1; i < backlog; i++)
                    {
                        int targetIdx = index - (1 + i);
                        if (targetIdx >= 0 && targetIdx < Arrows.Length && Arrows[targetIdx] != null)
                        {
                            Arrows[targetIdx].SetActive(false);
                        }
                    }
                }
            }
            else
            {
                backlog++;
            }
            index++;
        }
        else
        {
            TutorialCanvas.SetActive(false);
            if (Arrows != null && MessageText.Length - 1 < Arrows.Length && Arrows[MessageText.Length - 1] != null)
            {
                Arrows[MessageText.Length - 1].SetActive(false);
            }
        }
    }
}
