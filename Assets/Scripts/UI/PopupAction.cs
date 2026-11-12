using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiKit;

// A row in the burnt-candle popup (Rebuild / Clear away): dark slot, key cap, label and a wax amount.
public class PopupAction : MonoBehaviour
{
    public UiPointer pointer;
    public CanvasGroup group;
    public Image background;
    public Image edge;
    public TextMeshProUGUI value;

    private bool hovered;
    private bool active = true;

    void Awake()
    {
        pointer.onEnter = () => { hovered = true; Paint(); };
        pointer.onExit = () => { hovered = false; Paint(); };
    }

    public void SetEnabled(bool on)
    {
        active = on;
        group.alpha = on ? 1f : 0.5f;
        Paint();
    }

    void Paint()
    {
        bool lit = active && hovered;
        background.color = lit ? SlotHover : SlotFill;
        edge.color = lit ? Brass : SlotEdge;
    }
}
