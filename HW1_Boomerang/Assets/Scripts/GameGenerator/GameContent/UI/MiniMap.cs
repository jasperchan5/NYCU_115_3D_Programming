using UnityEngine;
using UnityEngine.UI;

public class MiniMap : MonoBehaviour
{
    [Header("Camera")]
    public float height = 40f;
    public float orthoSize = 25f;
    public bool rotateWithPlayer = false;

    [Header("UI")]
    public Vector2 size = new Vector2(350f, 350f);
    public Vector2 anchoredPos = new Vector2(-20f, -20f);   
    public float borderThickness = 3f;
    public Color borderColor = new Color(1f, 1f, 1f, 0.8f);

    [Header("Player Marker")]
    public Color markerColor = new Color(0.2f, 1f, 0.3f, 1f);
    public float markerSize = 14f;

    Camera mapCam;
    RenderTexture rt;
    Transform target;
    RectTransform marker;

    void Start()
    {
        CreateCamera();
        BuildUI();
    }

    void LateUpdate()
    {
        if (!target)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm) target = pm.transform;
            return;
        }

        Vector3 p = target.position;
        mapCam.transform.position = new Vector3(p.x, p.y + height, p.z);

        mapCam.transform.rotation = rotateWithPlayer
            ? Quaternion.Euler(90f, target.eulerAngles.y, 0f)
            : Quaternion.Euler(90f, 0f, 0f);

        // 玩家標記的朝向
        if (marker)
            marker.localRotation = Quaternion.Euler(0f, 0f,
                rotateWithPlayer ? 0f : -target.eulerAngles.y);
    }

    void CreateCamera()
    {
        rt = new RenderTexture(512, 512, 16);
        rt.name = "MiniMapRT";

        var camGO = new GameObject("MiniMapCamera");
        mapCam = camGO.AddComponent<Camera>();
        mapCam.orthographic = true;
        mapCam.orthographicSize = orthoSize;
        mapCam.targetTexture = rt;
        mapCam.clearFlags = CameraClearFlags.SolidColor;
        mapCam.backgroundColor = new Color(0.1f, 0.12f, 0.15f, 1f);
        mapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        mapCam.depth = -10;                 // 低於主鏡頭
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("MiniMapCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 外框
        var border = CreateImage("Border", canvasGO.transform, borderColor);
        SetRect(border.rectTransform, new Vector2(1f, 1f), anchoredPos,
                size + Vector2.one * borderThickness * 2f);

        // 地圖畫面
        var map = CreateImage("Map", border.transform, Color.white);
        map.sprite = null;
        var rawGO = new GameObject("RawImage", typeof(RawImage));
        rawGO.transform.SetParent(border.transform, false);
        var raw = rawGO.GetComponent<RawImage>();
        raw.texture = rt;
        raw.raycastTarget = false;
        SetRect(raw.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Destroy(map.gameObject);

        // 玩家標記（中心的三角形，用方塊代替）
        var markerImg = CreateImage("PlayerMarker", border.transform, markerColor);
        SetRect(markerImg.rectTransform, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(markerSize * 0.5f, markerSize));
        marker = markerImg.rectTransform;
    }

    Image CreateImage(string imgName, Transform parent, Color c)
    {
        var go = new GameObject(imgName, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
    }

    void OnDestroy()
    {
        if (rt) rt.Release();
    }
}