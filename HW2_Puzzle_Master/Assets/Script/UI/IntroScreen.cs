using UnityEngine;
using UnityEngine.UI;

namespace PuzzleMaster
{
    /// 
    /// 開場畫面（作業要求 1. Introduction Screen）。全部用 uGUI 在執行時建立，不使用外部圖片。
    ///
    /// 版面（參考解析度 1920 x 1080，座標以畫面中心為原點）：
    ///     五顆水晶封印（菱形）      ← 故事裡的五個 Crystal Seals，依序一明一暗像波浪
    ///     PUZZLE MASTER             ← 標題，金色、外框 + 陰影、緩慢呼吸縮放
    ///     Explore · Fight · ...     ← 副標
    ///     ┌───────────────────┐
    ///     │ Explore the region. │   ← 作業指定的四行說明
    ///     │ ...                 │
    ///     └───────────────────┘
    ///     Press SPACE to Start      ← 閃爍
    ///     Press ESC to Quit
    ///     操作說明一列
    ///
    /// 開場時 Time.timeScale = 0，所以動畫一律用 Time.unscaledTime / unscaledDeltaTime。
    /// 按 SPACE 後（GameManager 切到 Playing）整個畫面在 0.4 秒內淡出，然後關閉。
    /// 
    public class IntroScreen : MonoBehaviour
    {
        CanvasGroup group;                      // 控制整個畫面的透明度（淡出用）
        RectTransform title;
        Text startText;
        Image[] seals = new Image[5], sealGlows = new Image[5];
        bool fadingOut;

        static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        static readonly Color Crystal = new Color(0.45f, 0.85f, 1f);
        static Font font;

        public static IntroScreen Create(GameManager gm)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 獨立的 Canvas，sortingOrder 設高一點 → 永遠蓋在遊戲 HUD 上面
            var go = new GameObject("IntroScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var intro = go.AddComponent<IntroScreen>();
            intro.group = go.GetComponent<CanvasGroup>();
            intro.Build();
            gm.StateChanged += intro.OnStateChanged;
            return intro;
        }

        void Build()
        {
            // 1. 背景：整片半透明深藍，遊戲世界在後面隱約看得到
            var bg = NewImage("Background", transform, new Color(0.03f, 0.04f, 0.09f, 0.88f));
            Stretch(bg.rectTransform);

            // 2. 五顆水晶封印：Image 不給 sprite 就是純色方塊，轉 45° 變成菱形
            //    後面墊一顆大一點、半透明的當作光暈
            for (int i = 0; i < 5; i++)
            {
                var pos = new Vector2((i - 2) * 90f, 330f);
                sealGlows[i] = NewImage("SealGlow", transform, Crystal);
                Place(sealGlows[i].rectTransform, pos, new Vector2(64f, 64f));
                sealGlows[i].rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
                seals[i] = NewImage("Seal", transform, Crystal);
                Place(seals[i].rectTransform, pos, new Vector2(36f, 36f));
                seals[i].rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            }

            // 3. 標題
            var titleText = NewText("Title", transform, "PUZZLE MASTER", 120, Gold, FontStyle.Bold);
            titleText.gameObject.AddComponent<Outline>().effectColor = new Color(0.35f, 0.18f, 0f, 1f);
            var shadow = titleText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(6f, -6f);
            title = titleText.rectTransform;
            Place(title, new Vector2(0f, 190f), new Vector2(1400f, 160f));

            var tagline = NewText("Tagline", transform, "Explore  ·  Fight  ·  Collect  ·  Solve  ·  Restore", 30, new Color(0.8f, 0.85f, 0.95f), FontStyle.Normal);
            Place(tagline.rectTransform, new Vector2(0f, 90f), new Vector2(1200f, 50f));

            // 4. 說明框：作業指定的四行文字
            var panel = NewImage("Panel", transform, new Color(0.08f, 0.1f, 0.18f, 0.9f));
            Place(panel.rectTransform, new Vector2(0f, -90f), new Vector2(820f, 250f));
            var border = panel.gameObject.AddComponent<Outline>();          // Outline 加在 Image 上 = 外框
            border.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
            border.effectDistance = new Vector2(2f, -2f);
            var body = NewText("Description", panel.transform,
                               "Explore the region.\nRecover monsters.\nCollect puzzle items.\nSolve all five puzzles before time runs out.",
                               34, Color.white, FontStyle.Normal);
            body.lineSpacing = 1.25f;
            Stretch(body.rectTransform);

            // 5. 開始 / 離開
            startText = NewText("Start", transform, "Press SPACE to Start", 48, Color.white, FontStyle.Bold);
            Place(startText.rectTransform, new Vector2(0f, -290f), new Vector2(900f, 70f));
            startText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);

            var quit = NewText("Quit", transform, "Press ESC to Quit", 28, new Color(0.7f, 0.72f, 0.8f), FontStyle.Normal);
            Place(quit.rectTransform, new Vector2(0f, -350f), new Vector2(900f, 40f));

            // 6. 操作說明（作業的 Controls 表格）
            var controls = NewText("Controls", transform,
                                   "WASD  Move        Mouse  Look Around        Left Mouse  Attack        M  Toggle Minimap        ESC  Quit Game",
                                   22, new Color(0.6f, 0.65f, 0.75f), FontStyle.Normal);
            Place(controls.rectTransform, new Vector2(0f, -460f), new Vector2(1800f, 40f));
        }

        void OnStateChanged(GameState s)
        {
            if (s == GameState.Playing) fadingOut = true;
        }

        void Update()
        {
            float t = Time.unscaledTime;

            // 標題：每 4 秒左右呼吸一次（±2%）
            title.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(t * 1.5f));

            // 「Press SPACE」：透明度在 0.35 ~ 1 之間閃爍
            var c = startText.color;
            c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 4f));
            startText.color = c;

            // 五顆封印：相位依序差 0.8 → 亮光像波浪一樣從左跑到右
            for (int i = 0; i < 5; i++)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 2.5f - i * 0.8f);      // 0 ~ 1
                seals[i].color = Color.Lerp(new Color(0.15f, 0.3f, 0.45f), Crystal, k);
                sealGlows[i].color = new Color(Crystal.r, Crystal.g, Crystal.b, 0.08f + 0.3f * k);
                sealGlows[i].rectTransform.localScale = Vector3.one * (1f + 0.25f * k);
            }

            // 淡出：0.4 秒內 alpha 1 → 0，結束後關掉整個畫面
            if (fadingOut)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime / 0.4f);
                if (group.alpha <= 0f) gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        // 建立 UI 的小工具
        // ------------------------------------------------------------------

        static Image NewImage(string name, Transform parent, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text NewText(string name, Transform parent, string content, int size, Color color, FontStyle style)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// 以畫面中心為原點擺放。
        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// 填滿父物件。
        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}