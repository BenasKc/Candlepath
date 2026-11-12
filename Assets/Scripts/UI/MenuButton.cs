using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiKit;

// Hover and disabled look for the menu buttons (main menu, map cards, pause), in the HUD's iron-and-brass style:
// a dark slot whose edge turns brass on hover, or a solid brass button for the main action.
// Set up by Candlepath > Apply Menu Theme, which also bakes the idle look into the scene / prefab.
[RequireComponent(typeof(Button))]
public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Same as the HUD's Start wave button
    public static readonly Color PrimaryFill = Hex("C9974E");
    public static readonly Color PrimaryHover = Hex("DDAA5C");
    public static readonly Color PrimaryInk = Hex("1A130D");
    public static readonly Color DisabledInk = Hex("9A8A70", 0.5f);

    [SerializeField] private bool primary;
    [SerializeField] private Image background;
    [SerializeField] private Image edge;
    [SerializeField] private TMP_Text label;

    private Button button;
    private bool hovered;
    private bool wasInteractable;

    public void Init(bool primary, Image background, Image edge, TMP_Text label)
    {
        this.primary = primary;
        this.background = background;
        this.edge = edge;
        this.label = label;
        Paint();
    }

    void OnEnable()
    {
        hovered = false;
        Paint();
    }

    // MapCard switches buttons off after they're enabled
    void Update()
    {
        if (Button.interactable != wasInteractable) Paint();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        Paint();
    }

    public void OnPointerExit(PointerEventData e)
    {
        hovered = false;
        Paint();
    }

    Button Button => button != null ? button : button = GetComponent<Button>();

    public void Paint()
    {
        if (background == null) return;
        bool on = wasInteractable = Button.interactable;
        bool hot = on && hovered;
        if (primary && on)
        {
            background.color = hot ? PrimaryHover : PrimaryFill;
            if (edge != null) edge.color = BrassBright;
            if (label != null) label.color = PrimaryInk;
        }
        else
        {
            background.color = hot ? SlotHover : SlotFill;
            if (edge != null) edge.color = hot ? Brass : SlotEdge;
            if (label != null) label.color = on ? TextLight : DisabledInk;
        }
    }
}
