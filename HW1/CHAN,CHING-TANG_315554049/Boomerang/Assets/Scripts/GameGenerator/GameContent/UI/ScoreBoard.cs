using UnityEngine;
using TMPro;

public class ScoreBoard : MonoBehaviour
{
    [Header("Layout")]
    public Vector2 anchoredPos = new Vector2(30f, -30f);
    public int fontSize = 28;
    public Color textColor = Color.white;

    TextMeshProUGUI label;

    void Start()
    {
        BuildUI();
        if (ScoreManager.Instance)
        {
            ScoreManager.Instance.OnScoreChanged += Refresh;
            Refresh();
        }
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance)
            ScoreManager.Instance.OnScoreChanged -= Refresh;
    }

    void Refresh()
    {
        var sm = ScoreManager.Instance;
        if (!sm || !label) return;

        label.text = $"Score: {sm.score}\n" +
                     $"Targets: {sm.targetsDestroyed}\n" +
                     $"Items: {sm.collectiblesCollected}";
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("ScoreCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var textGO = new GameObject("ScoreText");
        textGO.transform.SetParent(canvasGO.transform, false);

        label = textGO.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;

        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);   // Upper-left corner
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(300f, 120f);
    }
}