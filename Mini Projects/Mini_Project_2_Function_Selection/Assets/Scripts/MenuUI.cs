using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FunctionSelection
{
    /// 
    /// 所有畫面的 UI：主選單、設定、選角、示範影片、遊戲 HUD、暫停選單。
    /// 每個畫面是一個 panel，GameApp 切換狀態時只顯示對應的 panel。
    /// 
    public class MenuUI : MonoBehaviour
    {
        GameApp app;
        RectTransform mainPanel, settingsPanel, charPanel, demoPanel, hudPanel, pausePanel, winPanel;
        Button mainFirst, settingsFirst, charFirst, pauseFirst, winFirst;
        Text heroLabel, charName, charTitle, charDesc, hudHero, hudGems, hudHp, toast, demoCaption, winText;
        Image hpFill;
        readonly Image[] statFills = new Image[3];
        float toastTimer;

        public static MenuUI Create(GameApp app)
        {
            var canvas = UIFactory.CreateCanvas("UI", 10);
            var ui = canvas.gameObject.AddComponent<MenuUI>();
            ui.app = app;
            ui.BuildMain(canvas.transform);
            ui.BuildSettings(canvas.transform);
            ui.BuildCharacterSelect(canvas.transform);
            ui.BuildDemo(canvas.transform);
            ui.BuildHud(canvas.transform);
            ui.BuildPause(canvas.transform);
            ui.BuildWin(canvas.transform);
            ui.BuildToast(canvas.transform);
            return ui;
        }

        // =====================================================================
        // 主選單
        // =====================================================================
        void BuildMain(Transform canvas)
        {
            var bg = UIFactory.Panel("MainMenu", canvas, new Color(0.03f, 0.05f, 0.09f, 0.72f));
            mainPanel = bg.rectTransform;
            mainPanel.anchorMin = new Vector2(0, 0);
            mainPanel.anchorMax = new Vector2(0.38f, 1);
            mainPanel.offsetMin = mainPanel.offsetMax = Vector2.zero;

            var col = UIFactory.Rect("Column", mainPanel);
            UIFactory.Stretch(col, 70);
            UIFactory.VStack(col, 18, 0, TextAnchor.MiddleLeft);

            var title = UIFactory.Label(col, "MYSTIC VILLAGE", 76, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            UIFactory.Size(title, 100);
            var sub = UIFactory.Label(col, "3D Game Programming · Mini-Project 4.E.2 Function Selection", 24, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.75f));
            UIFactory.Size(sub, 40);
            UIFactory.Size(UIFactory.Rect("Gap", col), 30);

            mainFirst = UIFactory.Button(col, "Start Game", () => app.StartGame());
            UIFactory.Size(mainFirst, 76);
            UIFactory.Size(UIFactory.Button(col, "Character Select", () => app.EnterCharacterSelect()), 76);
            UIFactory.Size(UIFactory.Button(col, "Settings", () => app.EnterSettings()), 76);
            UIFactory.Size(UIFactory.Button(col, "Demo Video", () => app.EnterDemo()), 76);
            UIFactory.Size(UIFactory.Button(col, "Quit", () => app.Quit()), 76);

            UIFactory.Size(UIFactory.Rect("Gap", col), 24);
            heroLabel = UIFactory.Label(col, "", 26, TextAnchor.MiddleLeft, Color.white);
            UIFactory.Size(heroLabel, 40);
            var hint = UIFactory.Label(col, "Mouse, or arrow keys + Enter", 20, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.5f));
            UIFactory.Size(hint, 30);
        }

        // =====================================================================
        // 設定
        // =====================================================================
        void BuildSettings(Transform canvas)
        {
            var box = UIFactory.Panel("Settings", canvas, UIFactory.PanelColor);
            settingsPanel = box.rectTransform;
            UIFactory.Place(settingsPanel, new Vector2(0.5f, 0.5f), new Vector2(1080, 860));

            var col = UIFactory.Rect("Column", settingsPanel);
            UIFactory.Stretch(col, 50);
            UIFactory.VStack(col, 14);

            var title = UIFactory.Label(col, "SETTINGS", 54, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.Size(title, 80);

            // 解析度
            var row = UIFactory.Row(col, "Resolution");
            var res = UIFactory.Dropdown(row, SettingsManager.ResolutionLabels, SettingsManager.ResolutionIndex,
                                         i => SettingsManager.ResolutionIndex = i);
            UIFactory.Fill(res, 44);

            row = UIFactory.Row(col, "Fullscreen");
            var full = UIFactory.Toggle(row, "Enabled", SettingsManager.Fullscreen, v => SettingsManager.Fullscreen = v);
            UIFactory.Fill(full, 40);

            row = UIFactory.Row(col, "Graphics Quality");
            var q = UIFactory.Dropdown(row, SettingsManager.QualityLabels, SettingsManager.Quality, i => SettingsManager.Quality = i);
            UIFactory.Fill(q, 44);

            SliderRow(col, "Master Volume", 0f, 1f, SettingsManager.MasterVolume, v => SettingsManager.MasterVolume = v, v => $"{v * 100:0}%");
            SliderRow(col, "Music Volume", 0f, 1f, SettingsManager.MusicVolume, v => SettingsManager.MusicVolume = v, v => $"{v * 100:0}%");
            SliderRow(col, "Brightness", -2f, 2f, SettingsManager.Brightness, v => SettingsManager.Brightness = v, v => $"{v:+0.0;-0.0;0.0} EV");
            SliderRow(col, "Gamma", -0.5f, 0.5f, SettingsManager.Gamma, v => SettingsManager.Gamma = v, v => $"{v:+0.00;-0.00;0.00}");

            var note = UIFactory.Label(col, "Resolution and fullscreen take effect in the built game, not inside the Unity Editor.",
                                       20, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.55f));
            UIFactory.Size(note, 34);

            var buttons = UIFactory.Rect("Buttons", col);
            UIFactory.Size(buttons, 70);
            UIFactory.HStack(buttons, 30);
            UIFactory.Button(buttons, "Reset Audio & Image", () =>
            {
                SettingsManager.ResetDefaults();
                app.RebuildSettingsPanel();
            }, 26);
            settingsFirst = UIFactory.Button(buttons, "Back", () => app.LeaveSettings());
        }

        void SliderRow(Transform col, string label, float min, float max, float value,
                       System.Action<float> set, System.Func<float, string> fmt)
        {
            var row = UIFactory.Row(col, label);
            Text valueText = null;
            var s = UIFactory.Slider(row, min, max, value, v => { set(v); if (valueText != null) valueText.text = fmt(v); });
            UIFactory.Fill(s, 24);
            valueText = UIFactory.Label(row, fmt(value), 24, TextAnchor.MiddleRight, UIFactory.Accent);
            UIFactory.Size(valueText, 56, 130);
        }

        /// 重設後重建設定頁，讓所有滑桿顯示新的值。
        public void RebuildSettings()
        {
            var canvas = settingsPanel.parent;
            int sibling = settingsPanel.GetSiblingIndex();
            Destroy(settingsPanel.gameObject);
            BuildSettings(canvas);
            settingsPanel.SetSiblingIndex(sibling);
            settingsPanel.gameObject.SetActive(true);
            Focus(settingsFirst);
        }

        // =====================================================================
        // 選角
        // =====================================================================
        void BuildCharacterSelect(Transform canvas)
        {
            var box = UIFactory.Panel("CharacterSelect", canvas, UIFactory.PanelColor);
            charPanel = box.rectTransform;
            UIFactory.Place(charPanel, new Vector2(0.5f, 0f), new Vector2(1240, 330), new Vector2(0, 40));

            var col = UIFactory.Rect("Column", charPanel);
            UIFactory.Stretch(col, 28);
            UIFactory.VStack(col, 8);

            var header = UIFactory.Rect("Header", col);
            UIFactory.Size(header, 64);
            UIFactory.HStack(header, 20);
            charName = UIFactory.Label(header, "", 50, TextAnchor.MiddleRight, UIFactory.Accent, FontStyle.Bold);
            charTitle = UIFactory.Label(header, "", 32, TextAnchor.MiddleLeft, Color.white, FontStyle.Italic);

            charDesc = UIFactory.Label(col, "", 26, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.85f));
            UIFactory.Size(charDesc, 40);

            var stats = UIFactory.Rect("Stats", col);
            UIFactory.Size(stats, 70);
            UIFactory.HStack(stats, 30);
            string[] names = { "SPEED", "JUMP", "POWER" };
            for (int i = 0; i < 3; i++)
            {
                var cell = UIFactory.Rect(names[i], stats);
                UIFactory.VStack(cell, 4);
                UIFactory.Size(UIFactory.Label(cell, names[i], 22, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.7f)), 28);
                var barBg = UIFactory.Panel("Bar", cell, new Color(1, 1, 1, 0.15f));
                UIFactory.Size(barBg, 18);
                var fill = UIFactory.Panel("Fill", barBg.rectTransform, UIFactory.Accent);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                statFills[i] = fill;
            }

            var buttons = UIFactory.Rect("Buttons", col);
            UIFactory.Size(buttons, 72);
            UIFactory.HStack(buttons, 24);
            var prev = UIFactory.Button(buttons, "<  Prev", () => app.ChangeCharacter(-1));
            charFirst = UIFactory.Button(buttons, "Select", () => app.ConfirmCharacter());
            var next = UIFactory.Button(buttons, "Next  >", () => app.ChangeCharacter(1));
            var back = UIFactory.Button(buttons, "Back", () => app.EnterMainMenu());
            // 這一列的左右鍵留給「切換角色」（GameApp 處理），所以關掉 UI 的方向鍵導覽；
            // Enter 永遠按到 Select，滑鼠照常可以點任何按鈕
            foreach (var b in new[] { prev, charFirst, next, back })
                b.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        public void ShowCharacter(CharacterDef def, bool isCurrent)
        {
            charName.text = def.name;
            charTitle.text = def.title + (isCurrent ? "   (current)" : "");
            charDesc.text = def.description;
            float[] v = { def.StatSpeed, def.StatJump, def.StatPower };
            for (int i = 0; i < 3; i++)
                statFills[i].rectTransform.anchorMax = new Vector2(Mathf.Lerp(0.12f, 1f, v[i]), 1f);
        }

        // =====================================================================
        // 示範影片
        // =====================================================================
        void BuildDemo(Transform canvas)
        {
            demoPanel = UIFactory.Rect("Demo", canvas);
            UIFactory.Stretch(demoPanel);

            // 電影感的上下黑邊
            var top = UIFactory.Panel("BarTop", demoPanel, Color.black);
            top.rectTransform.anchorMin = new Vector2(0, 0.9f); top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.offsetMin = top.rectTransform.offsetMax = Vector2.zero;
            var bottom = UIFactory.Panel("BarBottom", demoPanel, Color.black);
            bottom.rectTransform.anchorMin = Vector2.zero; bottom.rectTransform.anchorMax = new Vector2(1, 0.1f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;

            demoCaption = UIFactory.Label(top.rectTransform, "", 28, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(demoCaption.rectTransform, 10);

            var skip = UIFactory.Button(bottom.rectTransform, "Skip  (Esc)", () => app.EnterMainMenu(), 26);
            UIFactory.Place((RectTransform)skip.transform, new Vector2(1f, 0.5f), new Vector2(240, 64), new Vector2(-40, 0));
        }

        public void SetDemoCaption(bool video)
        {
            demoCaption.text = video
                ? "DEMO VIDEO"
                : "LIVE DEMO  —  no video file found, showing real-time gameplay.  Put a recording at  StreamingAssets/demo.mp4  to play it here.";
        }

        // =====================================================================
        // 遊戲 HUD
        // =====================================================================
        void BuildHud(Transform canvas)
        {
            hudPanel = UIFactory.Rect("HUD", canvas);
            UIFactory.Stretch(hudPanel);

            var box = UIFactory.Panel("Status", hudPanel, UIFactory.PanelColor);
            UIFactory.Place(box.rectTransform, new Vector2(0, 1), new Vector2(420, 170), new Vector2(30, -30));
            var col = UIFactory.Rect("Column", box.rectTransform);
            UIFactory.Stretch(col, 18);
            UIFactory.VStack(col, 6, 0, TextAnchor.UpperLeft);
            hudHero = UIFactory.Label(col, "", 30, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            UIFactory.Size(hudHero, 38);
            hudHp = UIFactory.Label(col, "", 24, TextAnchor.MiddleLeft);
            UIFactory.Size(hudHp, 30);
            var hpBg = UIFactory.Panel("HpBar", col, new Color(1, 1, 1, 0.15f));
            UIFactory.Size(hpBg, 14);
            hpFill = UIFactory.Panel("Fill", hpBg.rectTransform, new Color(0.9f, 0.3f, 0.3f));
            hpFill.rectTransform.anchorMin = Vector2.zero;
            hpFill.rectTransform.anchorMax = Vector2.one;
            hpFill.rectTransform.offsetMin = hpFill.rectTransform.offsetMax = Vector2.zero;
            hudGems = UIFactory.Label(col, "", 26, TextAnchor.MiddleLeft);
            UIFactory.Size(hudGems, 34);

            var hint = UIFactory.Label(hudPanel, "WASD move   ·   Shift run   ·   Space jump   ·   Mouse look   ·   Wheel zoom   ·   Esc pause",
                                       22, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.7f));
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(1500, 40), new Vector2(0, 20));
        }

        public void UpdateHud(CharacterDef def, int hp, int maxHp, int gems, int totalGems, float seconds)
        {
            hudHero.text = $"{def.name}  ·  {def.title}";
            hudHp.text = $"HP  {hp} / {maxHp}";
            hpFill.rectTransform.anchorMax = new Vector2((float)hp / maxHp, 1f);
            hudGems.text = $"Gems  {gems} / {totalGems}      Time  {(int)seconds / 60:0}:{(int)seconds % 60:00}";
        }

        // =====================================================================
        // 暫停與過關
        // =====================================================================
        void BuildPause(Transform canvas)
        {
            pausePanel = UIFactory.Rect("Pause", canvas);
            UIFactory.Stretch(pausePanel);
            pausePanel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
            var box = UIFactory.Panel("Box", pausePanel, UIFactory.PanelColor);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(560, 470));
            var col = UIFactory.Rect("Column", box.rectTransform);
            UIFactory.Stretch(col, 40);
            UIFactory.VStack(col, 16);
            UIFactory.Size(UIFactory.Label(col, "PAUSED", 54, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold), 80);
            pauseFirst = UIFactory.Button(col, "Resume", () => app.Resume());
            UIFactory.Size(pauseFirst, 72);
            UIFactory.Size(UIFactory.Button(col, "Settings", () => app.EnterSettings()), 72);
            UIFactory.Size(UIFactory.Button(col, "Main Menu", () => app.EnterMainMenu()), 72);
        }

        void BuildWin(Transform canvas)
        {
            winPanel = UIFactory.Rect("Win", canvas);
            UIFactory.Stretch(winPanel);
            winPanel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
            var box = UIFactory.Panel("Box", winPanel, UIFactory.PanelColor);
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720, 420));
            var col = UIFactory.Rect("Column", box.rectTransform);
            UIFactory.Stretch(col, 40);
            UIFactory.VStack(col, 16);
            UIFactory.Size(UIFactory.Label(col, "ALL GEMS FOUND!", 50, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold), 76);
            winText = UIFactory.Label(col, "", 28, TextAnchor.MiddleCenter);
            UIFactory.Size(winText, 60);
            winFirst = UIFactory.Button(col, "Play Again", () => app.StartGame());
            UIFactory.Size(winFirst, 72);
            UIFactory.Size(UIFactory.Button(col, "Main Menu", () => app.EnterMainMenu()), 72);
        }

        public void ShowWin(float seconds, int hp)
        {
            winText.text = $"Time  {(int)seconds / 60:0}:{(int)seconds % 60:00}      HP left  {hp}";
            winPanel.gameObject.SetActive(true);
            Focus(winFirst);
        }

        // =====================================================================
        // 提示訊息
        // =====================================================================
        void BuildToast(Transform canvas)
        {
            toast = UIFactory.Label(canvas, "", 34, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UIFactory.Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(1400, 60), new Vector2(0, -60));
            var shadow = toast.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2, -2);
        }

        public void Toast(string message, float seconds = 2.5f)
        {
            toast.text = message;
            toastTimer = seconds;
        }

        void Update()
        {
            toastTimer -= Time.unscaledDeltaTime;
            var c = toast.color;
            c.a = Mathf.Clamp01(toastTimer * 2f);
            toast.color = c;
        }

        // =====================================================================
        // 切換畫面
        // =====================================================================
        public void Show(GameApp.State state)
        {
            mainPanel.gameObject.SetActive(state == GameApp.State.MainMenu);
            settingsPanel.gameObject.SetActive(state == GameApp.State.Settings);
            charPanel.gameObject.SetActive(state == GameApp.State.CharacterSelect);
            demoPanel.gameObject.SetActive(state == GameApp.State.Demo);
            hudPanel.gameObject.SetActive(state == GameApp.State.InGame || state == GameApp.State.Paused);
            pausePanel.gameObject.SetActive(state == GameApp.State.Paused);
            winPanel.gameObject.SetActive(false);

            switch (state)
            {
                case GameApp.State.MainMenu:
                    var d = CharacterCatalog.Selected;
                    heroLabel.text = $"Current hero:  {d.name}, {d.title}";
                    Focus(mainFirst);
                    break;
                case GameApp.State.Settings: Focus(settingsFirst); break;
                case GameApp.State.CharacterSelect: Focus(charFirst); break;
                case GameApp.State.Paused: Focus(pauseFirst); break;
                default: Focus(null); break;
            }
        }

        static void Focus(Selectable s)
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(s != null ? s.gameObject : null);
        }
    }
}
