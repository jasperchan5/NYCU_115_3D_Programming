using UnityEngine;
using UnityEngine.UI;

namespace FunctionSelection
{
    ///
    /// 劇情用的畫面元素（全部用 uGUI 在執行時建立）：
    ///   字幕      ：畫面下方，像電影字幕（白字黑邊）。說話的人名字用顏色標出來，旁白和腦中的聲音用斜體
    ///   標題卡    ：畫面正中央的大字（開場那句 "All Metal of God appear in pairs."）
    ///   目標欄    ：左上角，現在要做什麼 + 這一步要用到的操作
    ///   互動提示  ：畫面中下方，例如「[E] Mix the powder and drink」
    /// 字幕偏左放：右下角是小地圖，字幕不能蓋到它。
    ///
    public class StoryUI : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        Text subtitle, title, objective, prompt;
        CanvasGroup subtitleGroup, titleGroup, promptGroup;

        static readonly string HeroColor = "#FFCF5A";
        static readonly string VoiceColor = "#9FD8FF";

        public static StoryUI Create(Transform parent)
        {
            var go = new GameObject("StoryUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<StoryUI>();
            ui.Canvas = go.GetComponent<Canvas>();
            ui.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ui.Canvas.sortingOrder = 6;                     // 在鎔金術 HUD（5）上面、選單（10）下面
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            ui.Build();
            return ui;
        }

        void Build()
        {
            // 字幕：下方偏左，寬 900，太長會自動換行
            subtitle = NewText("Subtitle", 30, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(-200f, 70f), new Vector2(900f, 160f));
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitle.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();

            // 標題卡：正中央偏上
            title = NewText("Title", 54, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1600f, 100f));
            title.fontStyle = FontStyle.Italic;
            title.color = new Color(0.95f, 0.9f, 0.78f);
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;

            // 目標欄：左上角的半透明框。寬度固定 560，高度自動配合內容：
            //   VerticalLayoutGroup：三段文字由上往下排，四周留邊距，並且把寬度交給文字（文字才知道在哪裡換行）
            //   ContentSizeFitter  ：框的高度 = 內容需要的高度 → 文字再長、換再多行都不會超出框
            var box = new GameObject("Objective", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<Image>();
            box.transform.SetParent(transform, false);
            box.color = new Color(0.04f, 0.05f, 0.09f, 0.7f);
            box.raycastTarget = false;
            var rt = box.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);    // 釘在左上角，往下長
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(560f, 0f);
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = box.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = NewBoxText("Header", 18, box.transform);
            header.text = "OBJECTIVE";
            header.color = new Color(1f, 0.8f, 0.35f);
            header.fontStyle = FontStyle.Bold;
            objective = NewBoxText("Text", 22, box.transform);
            var controls = NewBoxText("Controls", 15, box.transform);
            controls.text = "MMB: wheel  1–8: quick burn  RMB: aim  Scroll: push / pull  Tab: skip line  Esc: pause";
            controls.color = new Color(1f, 1f, 1f, 0.55f);

            // 互動提示：畫面中下方，閃爍
            prompt = NewText("Prompt", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(-200f, 280f), new Vector2(900f, 50f));
            prompt.color = Color.white;
            promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
        }

        Text NewText(string name, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, Transform parent = null)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<Text>();
            t.transform.SetParent(parent != null ? parent : transform, false);
            t.font = UIFactory.Font;
            t.fontSize = size;
            t.alignment = align;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = anchor;
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = sizeDelta;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            return t;
        }

        /// 目標欄裡的文字：位置與大小交給 VerticalLayoutGroup，超過寬度就換行。
        Text NewBoxText(string name, int size, Transform box)
        {
            var t = NewText(name, size, TextAnchor.UpperLeft, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, box);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        // ------------------------------------------------------------------
        // 給 StoryDirector 呼叫
        // ------------------------------------------------------------------

        /// 角色台詞：「名字  “台詞”」，名字用金色。
        public void ShowHero(string name, string line) =>
            ShowSubtitle($"<color={HeroColor}><b>{name}</b></color>   “{line}”");

        /// 腦中的聲音：名字和台詞都是斜體、淡藍色。
        public void ShowVoice(string line) =>
            ShowSubtitle($"<color={VoiceColor}><i><b>Voice</b>   “{line}”</i></color>");

        /// 旁白：斜體、淡灰色，沒有名字。
        public void ShowNarration(string line) =>
            ShowSubtitle($"<color=#D8D8D8><i>{line}</i></color>");

        void ShowSubtitle(string richText) => subtitle.text = richText;

        public void SetTitle(string text) => title.text = text;
        public void SetTitleAlpha(float a) => titleGroup.alpha = a;
        public void SetSubtitleAlpha(float a) => subtitleGroup.alpha = a;
        public void SetObjective(string text) => objective.text = text;

        /// 互動提示：text = null 時隱藏；顯示時透明度在 0.55 ~ 1 之間閃爍。
        public void SetPrompt(string text)
        {
            if (text == null) { promptGroup.alpha = 0f; return; }
            prompt.text = text;
            promptGroup.alpha = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
        }
    }
}