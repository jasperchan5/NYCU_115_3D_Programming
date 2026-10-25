using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 右上角的操作說明面板：列出所有熱鍵，並即時顯示目前的速度 / 視角 / 游標狀態
// 按 H 可以收合 / 展開
public class ControlsHint : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.H;
    public bool startExpanded = true;

    [Header("Layout")]
    public Vector2 anchoredPos = new Vector2(-30f, -450f); 
    public int fontSize = 20;
    public float keyColumnWidth = 130f;                     // 熱鍵欄寬，說明文字從這裡開始對齊

    [Header("Colors")]
    public Color bgColor = new Color(0f, 0f, 0f, 0.45f);
    public Color textColor = Color.white;
    public Color keyColor = new Color(1f, 0.84f, 0.31f);    // 熱鍵：黃色
    public Color stateColor = new Color(0.4f, 0.85f, 1f);   // 目前狀態：淺藍

    CameraController camCtrl;
    PlayerMovement player;

    TextMeshProUGUI label;
    bool expanded;
    string lastText;

    // 場景裡有其他 HUD（ScoreBoard）卻沒掛這個元件時，自動補上，不必手動改場景
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindFirstObjectByType<ControlsHint>()) return;
        var hud = FindFirstObjectByType<ScoreBoard>();
        if (hud) hud.gameObject.AddComponent<ControlsHint>();
    }

    void Start()
    {
        expanded = startExpanded;
        BuildUI();
        Refresh();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) expanded = !expanded;

        // 玩家是 LevelSetup 在 Start 才生成的，所以這裡延遲去找
        if (!camCtrl)
            camCtrl = Camera.main ? Camera.main.GetComponent<CameraController>() : null;
        if (!camCtrl) camCtrl = FindFirstObjectByType<CameraController>();
        if (!player) player = FindFirstObjectByType<PlayerMovement>();

        Refresh();
    }

    void Refresh()
    {
        string text = expanded ? BuildFullText() : BuildCollapsedText();
        if (text == lastText) return;          // 內容沒變就不重排版
        lastText = text;
        label.text = text;
    }

    string BuildCollapsedText()
    {
        return Row(KeyName(toggleKey), "Show controls");
    }

    string BuildFullText()
    {
        // 熱鍵優先讀元件上的設定，Inspector 改了這裡也會跟著變
        KeyCode viewKey   = camCtrl ? camCtrl.toggleKey : KeyCode.V;
        KeyCode cursorKey = camCtrl ? camCtrl.cursorToggleKey : KeyCode.Tab;

        string speed  = player && player.IsFast ? "Run" : "Walk";
        string view   = camCtrl && camCtrl.mode == CameraController.Mode.FirstPerson ? "1st person" : "3rd person";
        string cursor = camCtrl && camCtrl.CursorFree ? "Free" : "Locked";
        bool thirdPerson = !camCtrl || camCtrl.mode == CameraController.Mode.ThirdPerson;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>CONTROLS</b>");
        sb.AppendLine(Row("W / S", "Move forward / back"));
        sb.AppendLine(Row("Mouse", "Turn / aim"));
        sb.AppendLine(Row("Left Click", "Throw boomerang at target"));
        sb.AppendLine(Row("F", "Jump"));
        sb.AppendLine(Row("Space", "Walk / run" + State(speed)));
        sb.AppendLine(Row(KeyName(viewKey), "Switch view" + State(view)));
        if (thirdPerson)
            sb.AppendLine(Row("Scroll", "Zoom camera"));
        sb.AppendLine(Row(KeyName(cursorKey), "Free / lock cursor" + State(cursor)));
        sb.AppendLine(Row("Esc", "Quit"));
        sb.Append(Row(KeyName(toggleKey), "Hide controls"));
        return sb.ToString();
    }

    string Row(string key, string action)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(keyColor)}>{key}</color>" +
               $"<pos={keyColumnWidth}>{action}";
    }

    string State(string s)
    {
        return $"  <color=#{ColorUtility.ToHtmlStringRGB(stateColor)}>[{s}]</color>";
    }

    static string KeyName(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Escape: return "Esc";
            case KeyCode.Space:  return "Space";
            case KeyCode.Return: return "Enter";
            default:             return k.ToString();
        }
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("ControlsHintCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 背景面板，錨定在右上角，大小隨文字自動調整
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGO.transform, false);
        var bg = panel.GetComponent<Image>();
        bg.color = bgColor;
        bg.raycastTarget = false;

        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 1f);
        prt.anchoredPosition = anchoredPos;

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 文字
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(panel.transform, false);
        label = textGO.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
    }
}