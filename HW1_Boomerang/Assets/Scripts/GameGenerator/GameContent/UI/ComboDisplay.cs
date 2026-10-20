using System.Collections;
using UnityEngine;
using TMPro;

// 一次投擲連續命中多個目標時，在畫面中央彈出 "X COMBO"。
// 跟 ScoreManager 一樣用單例，讓 Boomerang 直接回報。
public class ComboDisplay : MonoBehaviour
{
    public static ComboDisplay Instance { get; private set; }

    [Header("Trigger")]
    public int minCombo = 2;            // 幾連以上才顯示

    [Header("Layout")]
    // 中央偏上。壓在正中央會跟準心重疊，而且擋住玩家正在瞄的東西
    public Vector2 anchoredPos = new Vector2(0f, 140f);
    public int fontSize = 90;
    public float riseDistance = 60f;    // 淡出時往上飄多遠

    [Header("Timing")]
    public float popTime = 0.18f;       // 彈出
    public float holdTime = 0.7f;       // 停留
    public float fadeTime = 0.45f;      // 淡出
    public float popScale = 1.5f;       // 彈出時的起始放大倍率

    [Header("Colors by tier")]
    public Color color2 = new Color(1f, 0.95f, 0.75f);   // 2 連
    public Color color4 = new Color(1f, 0.62f, 0.18f);   // 4 連
    public Color color6 = new Color(1f, 0.30f, 0.25f);   // 6 連以上
    public Color outlineColor = new Color(0f, 0f, 0f, 0.85f);
    public float outlineWidth = 0.22f;

    TextMeshProUGUI label;
    CanvasGroup group;
    RectTransform rect;
    Coroutine anim;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(int combo)
    {
        if (combo < minCombo || !label) return;

        label.text = $"{combo} COMBO";
        label.color = TierColor(combo);

        // 重新觸發時要從頭來，不能讓上一次的淡出把新的吃掉
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate());
    }

    Color TierColor(int combo)
    {
        if (combo >= 6) return color6;
        if (combo >= 4) return color4;
        return color2;
    }

    IEnumerator Animate()
    {
        group.alpha = 1f;
        rect.anchoredPosition = anchoredPos;

        // 彈出：從放大的狀態收回原尺寸，ease-out 讓它一開始快、後面穩住
        float t = 0f;
        while (t < popTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popTime);
            float e = 1f - (1f - k) * (1f - k);
            rect.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, e);
            yield return null;
        }
        rect.localScale = Vector3.one;

        yield return new WaitForSeconds(holdTime);

        // 淡出時順便往上飄，比單純消失容易注意到
        t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeTime);
            group.alpha = 1f - k;
            rect.anchoredPosition = anchoredPos + Vector2.up * (riseDistance * k);
            yield return null;
        }

        group.alpha = 0f;
        anim = null;
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("ComboCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;          // 蓋在準心和其他 UI 之上

        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var textGO = new GameObject("ComboText");
        textGO.transform.SetParent(canvasGO.transform, false);

        label = textGO.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.color = color2;

        // 描邊，不然淺色字壓在淺色場景上會看不見
        label.outlineColor = outlineColor;
        label.outlineWidth = outlineWidth;

        rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(900f, 160f);

        group = textGO.AddComponent<CanvasGroup>();
        group.alpha = 0f;               // 一開始不顯示
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}