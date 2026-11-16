using UnityEngine;
using UnityEngine.UI;

// Shared by the HUD and menu scripts: the iron-and-brass palette for state colours (the shapes, fonts and layout
// live in the UI prefabs), the shake used on rejects, and the fold chevron. Use with `using static UiKit;`.
public static class UiKit
{
    public static readonly Color Brass = Hex("B48A4A");
    public static readonly Color BrassBright = Hex("E2B45A");
    public static readonly Color SlotFill = Hex("221B15");
    public static readonly Color SlotHover = Hex("2E251D");
    public static readonly Color SlotEdge = Hex("5C4831");
    public static readonly Color TextLight = Hex("ECE0C8");
    public static readonly Color TextDim = Hex("9A8A70");

    // Chevron points up ("fold away") when open, down ("bring back") when folded
    public static void SetFolded(Image chevron, bool folded) =>
        chevron.rectTransform.localEulerAngles = new Vector3(0, 0, folded ? 180f : 0f);

    // Damped side-to-side wobble; t runs from 1 down to 0
    public static float Shake(float t) => t <= 0f ? 0f : Mathf.Sin(t * 38f) * 7f * t * t;

    public static Color Hex(string hex, float alpha = 1f)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = alpha;
        return c;
    }
}
