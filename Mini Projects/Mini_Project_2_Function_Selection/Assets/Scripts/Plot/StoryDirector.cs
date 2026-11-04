using System.Collections.Generic;
using Allomancy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FunctionSelection
{
    ///
    /// 劇本第一幕「Starting Area」的流程控制：依玩家走到哪裡、做了什麼，播放字幕、更新目標、往下一步走。
    ///
    ///   Intro        開場標題卡 + 三句獨白                          → 一開始就播
    ///   ToRift       沿土路往北走                                    → 走到裂谷附近
    ///   AtBottle     看到酒瓶與金屬粉、腦中的聲音                    → 走到酒瓶旁邊
    ///   Drink        按 E 把金屬粉混進酒裡喝下 → 鐵、鋼存量變滿
    ///   BurnIron     燒鐵：藍線出現、聲音解說鐵拉                    → 開始燒鐵
    ///   CrossRift    瞄準對岸的橋柱鐵拉，飛過裂谷                    → 落在對岸
    ///   ToBarriers   往城牆走                                        → 走到木柵欄前
    ///   PushBarriers 聲音解說鋼推，推飛木柵欄                        → 走過柵欄
    ///   ClimbWall    想辦法越過城牆（烽火柱、牆上的鐵釘都可以）      → 落在城牆外面的地上
    ///   Done         最後一句旁白播完 → 過關
    ///
    /// 台詞裡的 {name} 會換成選到的角色名字（劇本裡的 Dean）。
    /// 字幕一句一句排隊播放；暫停時字幕也停住（只在 GameApp.Playing 時計時）。
    /// 掉進裂谷會回到最近的檢查點。
    ///
    public class StoryDirector : MonoBehaviour
    {
        enum Speaker { Title, Narration, Hero, Voice }
        enum Stage { ToRift, AtBottle, Drink, BurnIron, CrossRift, ToBarriers, PushBarriers, ClimbWall, Done }

        struct Line
        {
            public Speaker who;
            public string text;
            public Line(Speaker who, string text) { this.who = who; this.text = text; }
        }

        GameApp app;
        PlayerController player;
        AllomancySession allomancy;
        LevelInfo level;
        StoryUI ui;
        string hero;

        Stage stage;
        readonly Queue<Line> queue = new Queue<Line>();
        bool showing;                       // 現在有一句字幕正在顯示
        bool isTitle;                       // 正在顯示的是標題卡（畫面中央），不是下方字幕
        float lineTime, lineDuration;
        Vector3 checkpoint;
        bool barriersBlown, completed;

        const float FallHeight = -6f;       // 低於這個高度 = 掉進裂谷
        const float Fade = 0.3f;            // 字幕淡入 / 淡出秒數

        public static StoryDirector Begin(GameApp app, PlayerController player, AllomancySession allomancy, LevelInfo level)
        {
            var d = new GameObject("StoryDirector").AddComponent<StoryDirector>();
            d.transform.SetParent(app.transform, false);
            d.app = app;
            d.player = player;
            d.allomancy = allomancy;
            d.level = level;
            d.hero = player.def.name;
            d.ui = StoryUI.Create(d.transform);
            d.checkpoint = level.checkpointNear;
            level.storyProps.gameObject.SetActive(true);        // 重玩時把酒瓶和金屬粉放回去

            d.Title("All Metal of God appear in pairs.");
            d.Hero("I can't remember who I am.");
            d.Hero("But something within my body tells me to go to that castle.");
            d.Hero("Let's go.");
            d.Goal(Stage.ToRift, "Follow the dirt path north, toward the castle.");
            return d;
        }

        public void End()
        {
            level.storyProps.gameObject.SetActive(true);        // 回主選單時，村莊恢復原狀
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------
        // 排字幕
        // ------------------------------------------------------------------

        void Title(string s) => queue.Enqueue(new Line(Speaker.Title, s));
        void Narrate(string s) => queue.Enqueue(new Line(Speaker.Narration, s));
        void Hero(string s) => queue.Enqueue(new Line(Speaker.Hero, s));
        void Voice(string s) => queue.Enqueue(new Line(Speaker.Voice, s));

        /// 字幕都播完了（沒有正在顯示的、也沒有排隊的）。
        bool Quiet => !showing && queue.Count == 0;

        void Goal(Stage next, string objective)
        {
            stage = next;
            ui.SetObjective(objective.Replace("{name}", hero));
        }

        /// 每句字幕顯示的秒數：越長越久（每個字 0.055 秒，最少 2.5 秒）；標題卡固定 4 秒。
        static float DurationOf(Line l) => l.who == Speaker.Title ? 4f : Mathf.Max(2.5f, 1.2f + 0.055f * l.text.Length);

        void UpdateSubtitles(float dt)
        {
            if (!showing && queue.Count > 0)
            {
                var l = queue.Dequeue();
                string text = l.text.Replace("{name}", hero);
                switch (l.who)
                {
                    case Speaker.Title: ui.SetTitle(text); break;
                    case Speaker.Narration: ui.ShowNarration(text); break;
                    case Speaker.Hero: ui.ShowHero(hero, text); break;
                    case Speaker.Voice: ui.ShowVoice(text); break;
                }
                showing = true;
                lineTime = 0f;
                lineDuration = DurationOf(l);
                isTitle = l.who == Speaker.Title;
            }
            if (!showing) { ui.SetSubtitleAlpha(0f); ui.SetTitleAlpha(0f); return; }

            // 淡入 → 停留 → 淡出：alpha = min(已過時間, 剩餘時間) / Fade，最大 1
            lineTime += dt;
            // Enter：跳過這一句。直接跳到「開始淡出」的時間點，0.3 秒後換下一句
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
                lineTime = Mathf.Max(lineTime, lineDuration - Fade);
            float a = Mathf.Clamp01(Mathf.Min(lineTime, lineDuration - lineTime) / Fade);
            if (isTitle) { ui.SetTitleAlpha(a); ui.SetSubtitleAlpha(0f); }
            else { ui.SetSubtitleAlpha(a); ui.SetTitleAlpha(0f); }
            if (lineTime >= lineDuration) showing = false;
        }

        // ------------------------------------------------------------------
        // 每幀：檢查玩家的位置與動作，推進劇情
        // ------------------------------------------------------------------

        void Update()
        {
            // 設定畫面時把劇情 UI 藏起來；遊戲中、暫停時顯示
            ui.Canvas.enabled = app.Current == GameApp.State.InGame || app.Current == GameApp.State.Paused;
            if (!app.Playing) { ui.SetPrompt(null); return; }

            UpdateSubtitles(Time.unscaledDeltaTime);

            var pos = player.transform.position;
            var reserves = allomancy.Reserves;

            // 掉進裂谷：回到檢查點
            if (pos.y < FallHeight)
            {
                player.Teleport(checkpoint);
                app.Toast($"{hero} fell into the rift…", 2f);
            }

            // 通關：落在城牆外面（北邊）的地上。不管用什麼方法、在哪個階段飛過去都算（喝下溶液之後）
            if (stage >= Stage.BurnIron && stage < Stage.Done && OutsideWall(pos))
            {
                Narrate("{name} flew over the wall, and landed on some stone bricks.");
                Goal(Stage.Done, "Chapter complete.");
            }

            string prompt = null;
            switch (stage)
            {
                case Stage.ToRift:
                    if (pos.z > LevelBuilder.RiftNearZ - 14f)
                    {
                        Narrate("{name} walked along the dirt path. A huge rift appeared ahead.");
                        Narrate("There was only a broken bridge. There was no way to pass.");
                        Goal(Stage.AtBottle, "Look around near the broken bridge.");
                    }
                    break;

                case Stage.AtBottle:
                    if (Near(level.storyProps.position, 4f))
                    {
                        Narrate("A thrown alcohol bottle lay on the ground. There was still some leftover in it.");
                        Narrate("Two bags were scattered beside it. Inside: gray, shining powders.");
                        Narrate("Some voices appeared in {name}'s head, as if they were unlocked.");
                        Voice("Mix the metal powder into the alcohol. Drink it.");
                        Hero("What? I… Who is talking to me?");
                        Narrate("No response.");
                        Hero("Perhaps this helps me to remember something.");
                        Goal(Stage.Drink, "Mix the metal powder into the alcohol and drink it.");
                    }
                    break;

                case Stage.Drink:
                    // 台詞播完、而且站在酒瓶旁邊才能喝
                    if (Quiet && Near(level.storyProps.position, 3f))
                    {
                        prompt = "[E]  Mix the powder into the alcohol and drink";
                        var kb = Keyboard.current;
                        if (kb != null && kb.eKey.wasPressedThisFrame)
                        {
                            allomancy.EmitFlare(new Color(0.75f, 0.78f, 0.85f), 30);   // 金屬粉的灰色，從胸口噴出
                            level.storyProps.gameObject.SetActive(false);
                            allomancy.GrantIronAndSteel();
                            SoundFx.Play(SoundFx.Kind.Pickup);
                            Narrate("{name} drank the solution without thinking twice.");
                            Narrate("Something heavy settled inside the stomach. An invisible force could touch it, and burn it.");
                            Goal(Stage.BurnIron, "Burn iron: hold the middle mouse button and drag to IRON, or press 1.");
                        }
                    }
                    break;

                case Stage.BurnIron:
                    if (reserves.IsBurning(Metal.Iron))
                    {
                        Narrate("{name} burnt some iron. Blue lines shot out of {name}'s chest, pointing to the nails on the huge wall.");
                        Voice("When you burn iron, you see all metal sources in your sight. Aim at the metal sources to pull. " +
                              "If the source is heavier than you, you are pulled toward, and vice versa.");
                        Goal(Stage.CrossRift, "Hold the right mouse button to aim at a bridge pole across the rift. Scroll down: Iron pull.");
                    }
                    break;

                case Stage.CrossRift:
                    if (pos.z > LevelBuilder.RiftFarZ + 1f && player.IsGrounded)
                    {
                        checkpoint = level.checkpointFar;
                        Narrate("{name} flew over the rift, and landed on the opposite side safely.");
                        Goal(Stage.ToBarriers, "Head toward the wall.");
                    }
                    break;

                case Stage.ToBarriers:
                    if (pos.z > LevelBuilder.BarrierZ - 12f)
                    {
                        Narrate("{name} saw some wooden barriers with metal spikes this time.");
                        Hero("Why are there so many obstacles?");
                        Voice("When you burn steel, you see all metal sources in your sight. Aim at the metal sources to push. " +
                              "If the source is heavier than you, you are pushed backward, and vice versa.");
                        Goal(Stage.PushBarriers, "Burn steel (press 2). Aim at a barrier. Scroll up: Steel push.");
                    }
                    break;

                case Stage.PushBarriers:
                    if (!barriersBlown && AnyBarrierMoved())
                    {
                        barriersBlown = true;
                        Narrate("{name} pushed the barriers, and they were blown away.");
                    }
                    if (pos.z > LevelBuilder.BarrierZ + 2.5f)
                    {
                        Hero("Finally, the wall. How can I climb it?");
                        Goal(Stage.ClimbWall, "Get over the wall. Try the iron beacon on top of it: aim, then scroll down to pull.");
                    }
                    break;

                case Stage.Done:
                    if (Quiet && !completed)
                    {
                        completed = true;
                        app.CompleteLevel();
                    }
                    break;
            }
            ui.SetPrompt(prompt);
        }

        /// 站在城牆北邊的地上：z 超過牆的背面，而且腳踩在地面附近（不是站在牆頂）。
        bool OutsideWall(Vector3 pos) =>
            pos.z > LevelBuilder.WallZ + LevelBuilder.WallThick + 0.5f && pos.y < 1f && player.IsGrounded;

        bool Near(Vector3 p, float r)
        {
            var d = player.transform.position - p;
            d.y = 0f;
            return d.sqrMagnitude < r * r;
        }

        /// 任何一個木柵欄正在快速移動（被鋼推推飛了）。
        bool AnyBarrierMoved()
        {
            foreach (var b in allomancy.Layout.barriers)
                if (b != null && b.linearVelocity.magnitude > 3f) return true;
            return false;
        }

        /// 小地圖上的目標（金點）：現在這一步要去的地方。
        public IEnumerable<Vector3> ObjectiveMarkers()
        {
            switch (stage)
            {
                case Stage.ToRift:
                case Stage.AtBottle:
                case Stage.Drink:
                    yield return level.storyProps.position; break;
                case Stage.BurnIron:
                case Stage.CrossRift:
                    yield return level.poleLanding; break;
                case Stage.ToBarriers:
                case Stage.PushBarriers:
                    yield return new Vector3(0f, 0f, LevelBuilder.BarrierZ); break;
                case Stage.ClimbWall:
                    yield return new Vector3(0f, 0f, LevelBuilder.WallZ + LevelBuilder.WallThick + 6f); break;   // 城牆後面
            }
        }
    }
}