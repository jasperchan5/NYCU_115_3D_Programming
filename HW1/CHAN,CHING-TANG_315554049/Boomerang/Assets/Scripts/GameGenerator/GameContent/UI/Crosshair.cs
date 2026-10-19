using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    [Header("Appearance")]
    public float size = 16f;
    public float thickness = 2f;
    public Color color = new Color(1f, 1f, 1f, 0.8f);
    public bool crossStyle = true;

    void Start()
    {
        var canvasGO = new GameObject("CrosshairCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        if (crossStyle)
        {
            CreateBar(canvasGO.transform, new Vector2(size, thickness));
            CreateBar(canvasGO.transform, new Vector2(thickness, size));
        }
        else
        {
            CreateBar(canvasGO.transform, new Vector2(thickness * 2f, thickness * 2f));
        }
    }

    void CreateBar(Transform parent, Vector2 sizeDelta)
    {
        var go = new GameObject("Bar", typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;          // 不擋滑鼠點擊

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = sizeDelta;
    }
}