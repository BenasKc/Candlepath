using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiKit;

// Credits screen of the main menu, built from code (no scene wiring): the team, then the sources of the music,
// sound effects, textures, 3D models and fonts. Keep in sync with Assets/Music/LICENSES.md,
// Assets/Sfx/LICENSES.md and Art/Source/polyhaven/README.md. Esc or the Back button returns to the menu.
public static class CreditsPanel
{
    const string Team =
        "Benas Kučinskas\nEimantas Labžentis\nNojus Cininas\nAugustas Kniška";

    // TODO: confirm the exact origin of Assets/Sfx/CandleLightSfx.mp3 (added without a note).
    // CC BY / CC BY-NC sounds must be credited by name; the BBC licence requires its own credit line.
    const string Sfx =
        "<i>Lights a candle light with a match</i> by straget\n" +
        "<size=70%><color=#9A8A70>CC BY 4.0, freesound.org/s/419504</color></size>\n" +
        "<i>Opening and closing Candle.wav</i> by 221313\n" +
        "<size=70%><color=#9A8A70>CC BY-NC 4.0, freesound.org/s/655549</color></size>\n" +
        "<i>lighting a candle</i> by Angelina.mandzavinos\n" +
        "<size=70%><color=#9A8A70>CC BY-NC 4.0, freesound.org/s/764851</color></size>\n" +
        "<i>Lighting+killing_candles_.wav</i> by AikenOli4\n" +
        "<size=70%><color=#9A8A70>CC BY-NC 4.0, freesound.org/s/595403</color></size>\n" +
        "Fire sound effects NHU05002102, 07002057, 07059050\n" +
        "<size=70%><color=#9A8A70>bbc.co.uk – © copyright BBC, sound-effects.bbcrewind.co.uk</color></size>\n" +
        "Further sounds by unfa, ReeseEarl6910, jptalty, staylor8, pcgeek86, SlowMoManVL, peppermintlark, " +
        "fryzu82, devilqube, deadrobotmusic, roisin.gleeson, shelbyshark, JacobEmanuelMadrid\n" +
        "<size=70%><color=#9A8A70>CC0, from freesound.org</color></size>";

    const string Music =
        "<i>Dark Cavern Ambient</i> by Paul Wortmann (Rusys)\n" +
        "<i>Exploration Theme</i> by megaconde (Biblioteka)\n" +
        "<i>Dark Urban Church Theme Loop</i> by beardalaxy (Koplyčia)\n" +
        "<size=70%><color=#9A8A70>CC0, from OpenGameArt.org</color></size>";

    const string Textures =
        "Rock Tile Floor, Stone Floor, Dark Wooden Planks, Wooden Floor 02, Stone Tiles, Floor Tiles 06, Old Planks 02\n" +
        "by Charlotte Baglioni, Dimitrios Savva, Amal Kumar, Rico Cilliers, Christopher Melani, Rob Tuytel\n" +
        "<size=70%><color=#9A8A70>CC0, from Poly Haven (polyhaven.com)</color></size>";

    const string Models =
        "Decoration props rendered to sprites from Poly Haven models by Ulan Cabanilla, Gabriel Radić, John Malcolm, " +
        "James Ray Cock, Jurita Burger, Kirill Sannikov, Rico Cilliers, Tina, Fran Calvente, Jake Mobley, " +
        "Riley Queen, Monsta3D, Josh Dean\n" +
        "<size=70%><color=#9A8A70>CC0, from Poly Haven (polyhaven.com)</color></size>";

    const string Fonts =
        "Cinzel (The Cinzel Project Authors)\n" +
        "IM Fell English (Igino Marini)\n" +
        "Alegreya Sans SC (The Alegreya Sans Project Authors)\n" +
        "Grenze (The Grenze Project Authors)\n" +
        "Liberation Sans (Red Hat, Inc.)\n" +
        "<size=70%><color=#9A8A70>SIL Open Font License 1.1</color></size>";

    const string Art =
        "Candles, enemies and the Eternal Lamp were modelled and rendered by the team in Blender.";

    public static GameObject Build(Transform parent, TMP_Text fontSource, Action onBack)
    {
        var root = MakeRect("Credits", parent);
        Stretch(root);
        var backdrop = root.gameObject.AddComponent<Image>();
        backdrop.color = Hex("0F0C0A");

        var title = MakeText("Title", root, fontSource, "Credits", 64, BrassBright, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(0, 90));

        // Scrolling list between the title and the back button
        var view = MakeRect("Viewport", root);
        Anchor(view, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 30), new Vector2(1000, -320));
        view.pivot = new Vector2(0.5f, 0.5f); // 190 px clear at the bottom, 130 px at the top
        view.gameObject.AddComponent<RectMask2D>();
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 40f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var content = MakeRect("Content", view);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 8;
        layout.padding = new RectOffset(20, 20, 10, 40);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = view;
        scroll.content = content;

        Section(content, fontSource, "Developers", Team);
        Section(content, fontSource, "Music", Music);
        Section(content, fontSource, "Sound effects", Sfx);
        Section(content, fontSource, "Textures", Textures);
        Section(content, fontSource, "3D models", Models);
        Section(content, fontSource, "Graphics", Art);
        Section(content, fontSource, "Fonts", Fonts);

        // Back button
        var edge = MakeRect("Btn_Back", root);
        Anchor(edge, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(284, 64));
        var edgeImg = edge.gameObject.AddComponent<Image>();
        var fill = MakeRect("Fill", edge);
        Stretch(fill);
        fill.offsetMin = new Vector2(2, 2);
        fill.offsetMax = new Vector2(-2, -2);
        var fillImg = fill.gameObject.AddComponent<Image>();
        var label = MakeText("Text", fill, fontSource, "Back", 30, TextLight, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        var button = edge.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = fillImg;
        button.onClick.AddListener(() => onBack());
        edge.gameObject.AddComponent<MenuButton>().Init(false, fillImg, edgeImg, label);

        root.gameObject.AddComponent<CreditsEscape>().onBack = onBack;
        return root.gameObject;
    }

    static void Section(RectTransform parent, TMP_Text font, string heading, string body)
    {
        var h = MakeText(heading, parent, font, heading.ToUpperInvariant(), 28, Brass, TextAlignmentOptions.Center);
        h.margin = new Vector4(0, 18, 0, 0);
        MakeText(heading + "Body", parent, font, body, 26, TextLight, TextAlignmentOptions.Top);
    }

    internal static TMP_Text MakeText(string name, Transform parent, TMP_Text font, string text, float size, Color color,
        TextAlignmentOptions align)
    {
        var t = MakeRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font.font; t.fontSharedMaterial = font.fontSharedMaterial; }
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    internal static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Anchored strip: pos is the anchored position, size the size (negative height on a stretched axis insets it)
    internal static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, min.y == 1 ? 1 : 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}

// Esc goes back while the credits are open
public class CreditsEscape : MonoBehaviour
{
    public Action onBack;

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            onBack?.Invoke();
    }
}
