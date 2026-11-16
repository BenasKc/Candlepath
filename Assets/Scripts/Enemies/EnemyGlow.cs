using UnityEngine;

public class EnemyGlow : MonoBehaviour
{
    const string ShaderPath = "Shaders/EnemyGlow";

    static Material material;
    static bool missing;

    private SpriteRenderer body, glow;

    void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        Material mat = SharedMaterial();
        if (body == null || mat == null) { enabled = false; return; }

        var go = new GameObject("EyeGlow");
        go.transform.SetParent(transform, false);
        glow = go.AddComponent<SpriteRenderer>();
        glow.sharedMaterial = mat;
        glow.sortingLayerID = body.sortingLayerID;
        glow.sortingOrder = body.sortingOrder + 1;
    }

    void LateUpdate()
    {
        // Mirror the animated frame and the health fade
        if (glow.sprite != body.sprite) glow.sprite = body.sprite;
        glow.flipX = body.flipX;
        glow.flipY = body.flipY;
        glow.color = new Color(1f, 1f, 1f, body.color.a);
        glow.enabled = body.enabled;
    }

    static Material SharedMaterial()
    {
        if (material != null || missing) return material;
        var shader = Resources.Load<Shader>(ShaderPath);
        if (shader == null || !shader.isSupported)
        {
            missing = true;
            Debug.LogWarning("EnemyGlow: shader Resources/" + ShaderPath + " not found or unsupported; eyes won't glow.");
            return null;
        }
        material = new Material(shader) { name = "EnemyGlow (runtime)", hideFlags = HideFlags.DontSave };
        return material;
    }
}
