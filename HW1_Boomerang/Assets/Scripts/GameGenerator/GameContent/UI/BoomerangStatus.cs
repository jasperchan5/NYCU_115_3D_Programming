using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class BoomerangStatus : MonoBehaviour
{
    [Header("Layout")]
    public Vector2 anchoredPos = new Vector2(30f, 30f);
    public float barWidth = 240f;
    public float barHeight = 24f;

    [Header("Colors")]
    public Color readyColor = new Color(0.3f, 0.9f, 0.4f, 0.9f);
    public Color cooldownColor = new Color(0.95f, 0.75f, 0.2f, 0.9f);
    public Color flyingColor = new Color(0.4f, 0.7f, 1f, 0.9f);
    public Color bgColor = new Color(0f, 0f, 0f, 0.45f);

    Throw thrower;
    Image fill;
    TextMeshProUGUI label;


    void Start()
    {
        BuildUI();
    }

    void Update()
    {
        if (!thrower)
        {
            thrower = FindFirstObjectByType<Throw>();
            if (!thrower) return;
        }

        switch (thrower.State)
        {
            case Throw.ThrowState.Flying:
                fill.fillAmount = 1f;
                fill.color = flyingColor;
                label.text = "Boomerang: In flight";
                break;

            case Throw.ThrowState.Cooldown:
                fill.fillAmount = thrower.CooldownProgress;
                fill.color = cooldownColor;
                label.text = $"Cooldown  {thrower.CooldownRemaining:F1}s";
                break;

            default:
                fill.fillAmount = 1f;
                fill.color = readyColor;
                label.text = "Boomerang: Ready";
                break;
        }
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("BoomerangStatusCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 容器，錨定在左下角
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = Vector2.zero;
        prt.anchoredPosition = anchoredPos;
        prt.sizeDelta = new Vector2(barWidth, barHeight + 26f);

        // 背景條
        var bg = CreateImage("Background", panel.transform, bgColor);
        SetRect(bg.rectTransform, Vector2.zero, new Vector2(barWidth, barHeight));

        // 填充條
        fill = CreateImage("Fill", panel.transform, readyColor);
        SetRect(fill.rectTransform, Vector2.zero, new Vector2(barWidth, barHeight));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        // 文字
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(panel.transform, false);
        label = textGO.AddComponent<TextMeshProUGUI>();
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.raycastTarget = false;
        SetRect(label.rectTransform, new Vector2(0f, barHeight + 4f),
                new Vector2(barWidth, 22f));
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

    void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}