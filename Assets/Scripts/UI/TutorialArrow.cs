using UnityEngine;
using UnityEngine.UI;

// Keeps a tutorial arrow next to the HUD element it explains, so it lands in the right place at any screen size
// and follows the HUD when its layout changes. The arrow sprite points right; the arrow sits on `side` of the
// target and points at it, bobbing gently. Sizes are in HUD units (1920x1080 reference), like the HUD itself.
[RequireComponent(typeof(RectTransform))]
public class TutorialArrow : MonoBehaviour
{
    public enum Target { Wax, CandleCard, CandleCost, CandleFold, BurntBadge, Flame, Wave, StartWave, AutoWaves }
    public enum Side { Left, Right, Above, Below }

    public Target target;
    [Tooltip("Which candle card (0 = first), for CandleCard and CandleCost")]
    public int cardIndex;
    [Tooltip("Which side of the target the arrow sits on; it points back at the target")]
    public Side side = Side.Left;
    [Tooltip("Space between the arrow tip and the target, in HUD units")]
    public float gap = 6f;
    [Tooltip("How far the arrow bobs toward the target, in HUD units")]
    public float bob = 6f;

    private RectTransform rect;
    private Graphic graphic;
    private CandlePanel panel;
    private GameHud hud;
    private readonly Vector3[] corners = new Vector3[4];

    void Awake()
    {
        rect = (RectTransform)transform;
        graphic = GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = false; // never block clicks on the HUD underneath
    }

    void OnEnable() => Place();
    void LateUpdate() => Place();

    RectTransform Resolve()
    {
        switch (target)
        {
            case Target.Flame:
            case Target.Wave:
            case Target.StartWave:
            case Target.AutoWaves:
                if (hud == null) hud = FindFirstObjectByType<GameHud>(FindObjectsInactive.Include);
                if (hud == null) return null;
                return target == Target.Flame ? hud.FlameTarget
                     : target == Target.Wave ? hud.WaveTarget
                     : target == Target.StartWave ? hud.StartWaveTarget
                     : hud.AutoWavesTarget;
            default:
                if (panel == null) panel = FindFirstObjectByType<CandlePanel>(FindObjectsInactive.Include);
                if (panel == null) return null;
                return target == Target.Wax ? panel.WaxTarget
                     : target == Target.CandleCard ? panel.CardTarget(cardIndex)
                     : target == Target.CandleCost ? panel.CostTarget(cardIndex)
                     : target == Target.CandleFold ? panel.FoldTarget
                     : panel.BurntTarget;
        }
    }

    void Place()
    {
        if (rect == null) return;
        RectTransform t = Resolve();
        Canvas hudCanvas = t != null ? t.GetComponentInParent<Canvas>() : null;
        Canvas ownCanvas = GetComponentInParent<Canvas>();
        bool show = t != null && t.gameObject.activeInHierarchy && hudCanvas != null && hudCanvas.enabled && ownCanvas != null;
        if (graphic != null) graphic.enabled = show;
        if (!show) return;

        hudCanvas = hudCanvas.rootCanvas;
        ownCanvas = ownCanvas.rootCanvas;

        // Same on-screen size as the HUD, whatever this canvas scales by
        float hudScale = hudCanvas.scaleFactor;
        var parent = rect.parent as RectTransform;
        float parentScale = parent != null ? parent.lossyScale.x : 1f;
        if (parentScale > 0f) rect.localScale = Vector3.one * (hudScale / parentScale);

        // Target edges on screen
        Camera hudCam = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
        t.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(hudCam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(hudCam, corners[2]);
        Vector2 mid = (min + max) * 0.5f;

        Vector2 dir;  // from the arrow toward the target
        Vector2 edge; // point on the target the arrow aims at
        switch (side)
        {
            case Side.Right: dir = Vector2.left; edge = new Vector2(max.x, mid.y); break;
            case Side.Above: dir = Vector2.down; edge = new Vector2(mid.x, max.y); break;
            case Side.Below: dir = Vector2.up; edge = new Vector2(mid.x, min.y); break;
            default: dir = Vector2.right; edge = new Vector2(min.x, mid.y); break;
        }

        float halfLength = rect.rect.width * 0.5f * hudScale;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        float distance = halfLength + (gap + bob * wave) * hudScale;
        Vector2 screen = edge - dir * distance;

        // Screen point -> this canvas
        Camera ownCam = ownCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ownCanvas.worldCamera;
        if (parent != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screen, ownCam, out Vector3 world))
            rect.position = world;
        rect.rotation = parent != null ? parent.rotation * Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg)
                                       : Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }
}
