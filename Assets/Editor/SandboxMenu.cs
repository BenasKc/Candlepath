using UnityEditor;
using UnityEngine;

// Candlepath > Sandbox Mode: toggles the in-editor sandbox (see SandboxController).
public static class SandboxMenu
{
    const string MenuPath = "Candlepath/Sandbox Mode";

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        bool on = !EditorPrefs.GetBool(SandboxController.PrefKey, false);
        EditorPrefs.SetBool(SandboxController.PrefKey, on);
        Debug.Log(on
            ? "Sandbox mode ON: press Play in a map scene."
            : "Sandbox mode OFF.");
    }

    [MenuItem(MenuPath, true)]
    static bool Validate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(SandboxController.PrefKey, false));
        return true;
    }
}
