using UnityEngine;

// Keeps the whole 16:9 map in view whatever the window / Game view shape is.
// At 16:9 or wider the camera keeps its authored size; on narrower windows (4:3, Free Aspect, a
// small docked Game view) it zooms out just enough that the map's full width fits, instead of
// cutting off the sides. Added to the main camera by TowerBuilder.
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [SerializeField] private float designAspect = 16f / 9f;

    private Camera cam;
    private float baseSize;
    private int lastW, lastH;

    void Awake()
    {
        cam = GetComponent<Camera>();
        baseSize = cam.orthographicSize; // the size the maps were laid out for at 16:9
    }

    void LateUpdate()
    {
        if (Screen.width == lastW && Screen.height == lastH) return;
        lastW = Screen.width;
        lastH = Screen.height;

        float aspect = (float)lastW / Mathf.Max(1, lastH);
        cam.orthographicSize = aspect >= designAspect ? baseSize : baseSize * designAspect / aspect;
    }
}
