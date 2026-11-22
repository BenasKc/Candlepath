using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UiKit;
using static CreditsPanel;

// Settings window (design document 5.7), built from code like the credits: Audio (master / music / effects /
// ambient volume), Video (resolution, fullscreen, vsync, brightness) and Controls (key rebinding).
// Opened from the main menu and the pause menu. Esc or Back closes it; while a key is being captured Esc cancels the capture.
public static class SettingsPanel
{
    public static bool IsOpen => SettingsController.IsOpen;

    public static GameObject Build(Transform parent, TMP_Text font, Action onClose)
    {
        var root = MakeRect("Settings", parent);
        Stretch(root);
        root.gameObject.AddComponent<Image>().color = Hex("0F0C0A");
        var controller = root.gameObject.AddComponent<SettingsController>();
        controller.onClose = onClose;

        var title = MakeText("Title", root, font, "Settings", 64, BrassBright, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50), new Vector2(0, 90));

        // Tabs
        var tabs = MakeRect("Tabs", root);
        Anchor(tabs, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(720, 56));
        var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 12;
        tabLayout.childControlWidth = tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = false;

        // Content area between the tabs and the back button
        var area = MakeRect("Content", root);
        // 190 px clear at the bottom (back button), 260 px at the top (title and tabs)
        Anchor(area, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -35), new Vector2(1000, -450));
        area.pivot = new Vector2(0.5f, 0.5f);

        var pages = new[]
        {
            Page(area, "Audio"), Page(area, "Video"), Page(area, "Controls"),
        };
        BuildAudio(pages[0], font);
        BuildVideo(pages[1], font);
        BuildControls(pages[2], font, controller);

        var tabButtons = new List<(MenuButton mb, Image fill, Image edge, TMP_Text label)>();
        string[] names = { "Audio", "Video", "Controls" };
        void Show(int index)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].gameObject.SetActive(i == index);
                var t = tabButtons[i];
                t.mb.Init(i == index, t.fill, t.edge, t.label);
            }
        }
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var b = MakeButton(tabs, font, names[i], 228, 56, () => Show(index), 26);
            tabButtons.Add(b);
        }
        Show(0);

        var back = MakeButton(root, font, "Back", 284, 64, () => controller.Close(), 30);
        var brt = (RectTransform)back.mb.transform;
        Anchor(brt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(284, 64));

        controller.Refresh();
        return root.gameObject;
    }

    // ----- Pages -----

    static RectTransform Page(RectTransform parent, string name)
    {
        var page = MakeRect(name, parent);
        Stretch(page);
        var v = page.gameObject.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.spacing = 12;
        v.padding = new RectOffset(20, 20, 20, 0);
        return page;
    }

    static void BuildAudio(RectTransform page, TMP_Text font)
    {
        AddSlider(Row(page, font, "Master volume"), font, GameSettings.Master, v => GameSettings.Master = v);
        AddSlider(Row(page, font, "Music"), font, GameSettings.Music, v => GameSettings.Music = v);
        AddSlider(Row(page, font, "Effects"), font, GameSettings.Sfx, v => GameSettings.Sfx = v);
        AddSlider(Row(page, font, "Ambient"), font, GameSettings.Ambient, v => GameSettings.Ambient = v);
    }

    static void BuildVideo(RectTransform page, TMP_Text font)
    {
        // Resolution: previous / next
        var res = Row(page, font, "Resolution");
        var list = GameSettings.Resolutions();
        int index = Mathf.Max(0, list.IndexOf(GameSettings.Resolution));
        TMP_Text resLabel = null;
        void SetRes(int i)
        {
            index = (i + list.Count) % list.Count;
            resLabel.text = $"{list[index].x} x {list[index].y}";
        }
        MakeButton(res, font, "<", 56, 48, () => { SetRes(index - 1); GameSettings.Resolution = list[index]; }, 28);
        resLabel = MakeText("Value", res, font, "", 26, TextLight, TextAlignmentOptions.Center);
        resLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 270;
        MakeButton(res, font, ">", 56, 48, () => { SetRes(index + 1); GameSettings.Resolution = list[index]; }, 28);
        SetRes(index);

        AddToggle(Row(page, font, "Fullscreen"), font, () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);
        AddToggle(Row(page, font, "Vertical sync"), font, () => GameSettings.VSync, v => GameSettings.VSync = v);

        var bright = Row(page, font, "Brightness");
        AddSlider(bright, font, GameSettings.Brightness, v => GameSettings.Brightness = v);
        var hint = MakeText("Hint", page, font, "The map is mostly dark. Raise brightness if you can't make out the path.",
            22, TextDim, TextAlignmentOptions.Center);
        hint.margin = new Vector4(0, 8, 0, 0);
    }

    static void BuildControls(RectTransform page, TMP_Text font, SettingsController controller)
    {
        foreach (BindAction action in Enum.GetValues(typeof(BindAction)))
        {
            var a = action;
            var row = Row(page, font, GameSettings.ActionName(a));
            var b = MakeButton(row, font, "", 220, 48, null, 26);
            b.mb.GetComponent<Button>().onClick.AddListener(() => controller.StartCapture(a));
            controller.keyLabels[a] = b.label;
        }
        var reset = Row(page, font, "");
        MakeButton(reset, font, "Reset to defaults", 300, 48, () => { GameSettings.ResetKeys(); controller.Refresh(); }, 24);
        var hint = MakeText("Hint", page, font, "Candle keys 1-9, Shift, Esc and Delete are reserved and can't be bound.", 22, TextDim, TextAlignmentOptions.Center);
        hint.margin = new Vector4(0, 8, 0, 0);
    }

    // ----- Controls -----

    // A settings row: label on the left, an empty container for the control on the right
    static RectTransform Row(RectTransform parent, TMP_Text font, string label)
    {
        var row = MakeRect(label + " Row", parent);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        h.spacing = 16;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

        var text = MakeText("Label", row, font, label, 28, TextLight, TextAlignmentOptions.MidlineLeft);
        text.gameObject.AddComponent<LayoutElement>().preferredWidth = 440;

        var control = MakeRect("Control", row);
        var c = control.gameObject.AddComponent<HorizontalLayoutGroup>();
        c.childAlignment = TextAnchor.MiddleLeft;
        c.childControlWidth = c.childControlHeight = true;
        c.childForceExpandWidth = c.childForceExpandHeight = false;
        c.spacing = 14;
        control.gameObject.AddComponent<LayoutElement>().preferredWidth = 520;
        return control;
    }

    static void AddSlider(RectTransform parent, TMP_Text font, float value, Action<float> onChange)
    {
        var s = MakeRect("Slider", parent);
        var le = s.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 380;
        le.preferredHeight = 40;

        var bg = MakeRect("Background", s);
        bg.anchorMin = new Vector2(0, 0.5f);
        bg.anchorMax = new Vector2(1, 0.5f);
        bg.offsetMin = new Vector2(0, -5);
        bg.offsetMax = new Vector2(0, 5);
        bg.gameObject.AddComponent<Image>().color = SlotFill;

        var fillArea = MakeRect("Fill Area", s);
        fillArea.anchorMin = new Vector2(0, 0.5f);
        fillArea.anchorMax = new Vector2(1, 0.5f);
        fillArea.offsetMin = new Vector2(12, -5);
        fillArea.offsetMax = new Vector2(-12, 5);
        var fill = MakeRect("Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0, 1);
        fill.sizeDelta = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = Brass;

        var handleArea = MakeRect("Handle Slide Area", s);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(12, 0);
        handleArea.offsetMax = new Vector2(-12, 0);
        var handle = MakeRect("Handle", handleArea);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = new Vector2(0, 1);
        handle.sizeDelta = new Vector2(24, 0);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = BrassBright;

        var label = MakeText("Value", parent, font, "", 26, TextDim, TextAlignmentOptions.MidlineRight);
        label.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;

        var slider = s.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        label.text = Mathf.RoundToInt(value * 100) + "%";
        slider.onValueChanged.AddListener(v =>
        {
            label.text = Mathf.RoundToInt(v * 100) + "%";
            onChange(v);
        });
    }

    static void AddToggle(RectTransform parent, TMP_Text font, Func<bool> get, Action<bool> set)
    {
        TMP_Text label = null;
        var b = MakeButton(parent, font, get() ? "On" : "Off", 160, 48, () =>
        {
            set(!get());
            label.text = get() ? "On" : "Off";
        }, 26);
        label = b.label;
    }

    // A menu-styled button (same look as the main menu's). Returns the pieces so callers can restyle it.
    static (MenuButton mb, Image fill, Image edge, TMP_Text label) MakeButton(Transform parent, TMP_Text font,
        string text, float width, float height, Action onClick, float fontSize)
    {
        var edgeRt = MakeRect("Btn_" + text, parent);
        var layout = edgeRt.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;
        edgeRt.sizeDelta = new Vector2(width, height);
        var edge = edgeRt.gameObject.AddComponent<Image>();
        var fillRt = MakeRect("Fill", edgeRt);
        Stretch(fillRt);
        fillRt.offsetMin = new Vector2(2, 2);
        fillRt.offsetMax = new Vector2(-2, -2);
        var fill = fillRt.gameObject.AddComponent<Image>();
        var label = MakeText("Text", fillRt, font, text, fontSize, TextLight, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        var button = edgeRt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = fill;
        if (onClick != null) button.onClick.AddListener(() => onClick());
        var mb = edgeRt.gameObject.AddComponent<MenuButton>();
        mb.Init(false, fill, edge, label);
        return (mb, fill, edge, label);
    }
}

// Esc closes the window; also captures the next key press while a binding is being changed
public class SettingsController : MonoBehaviour
{
    public Action onClose;
    public readonly Dictionary<BindAction, TMP_Text> keyLabels = new Dictionary<BindAction, TMP_Text>();

    static bool open;
    static int closedFrame = -1;

    // True while open and for the frame it closed, so the same Esc press doesn't also resume the pause menu
    public static bool IsOpen => open || closedFrame == Time.frameCount;

    private BindAction? capturing;

    void OnEnable()
    {
        open = true;
        capturing = null;
        Refresh();
    }

    void OnDisable()
    {
        open = false;
        closedFrame = Time.frameCount;
        PlayerPrefs.Save();
    }

    public void Close() => onClose?.Invoke();

    public void StartCapture(BindAction action)
    {
        capturing = action;
        Refresh();
    }

    public void Refresh()
    {
        foreach (var pair in keyLabels)
            pair.Value.text = capturing == pair.Key ? "Press a key..." : GameSettings.KeyName(GameSettings.GetKey(pair.Key));
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (capturing != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame) { capturing = null; Refresh(); return; }
            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame || key.keyCode == Key.None || key.keyCode == Key.Escape) continue;
                if (GameSettings.IsReserved(key.keyCode))
                {
                    // Keep listening and say why the key was refused
                    if (keyLabels.TryGetValue(capturing.Value, out var label))
                        label.text = GameSettings.KeyName(key.keyCode) + " is reserved";
                    return;
                }
                GameSettings.SetKey(capturing.Value, key.keyCode);
                capturing = null;
                Refresh();
                return;
            }
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame) Close();
    }
}
