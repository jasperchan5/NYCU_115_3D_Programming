using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace FunctionSelection
{
    /// 
    /// 進入點與狀態機。按下 Play 後自動建立整個遊戲（不需要在場景裡手動擺任何東西）：
    ///
    ///   MainMenu ──Start Game──────▶ InGame ◀──Esc──▶ Paused ──Settings──▶ Settings
    ///      │──Character Select──▶ CharacterSelect
    ///      │──Settings──────────▶ Settings
    ///      │──Demo Video────────▶ Demo
    ///      └──Quit
    ///
    /// 遊戲關卡（只有一關）= 劇本第一幕 Starting Area：
    ///   StartGame 時建立 AllomancySession（mini 1 的 Game UI）與 StoryDirector（劇情、字幕）；
    ///   劇情走完（站上城牆）→ CompleteLevel() 顯示過關畫面。
    /// 
    public class GameApp : MonoBehaviour
    {
        public enum State { MainMenu, Settings, CharacterSelect, Demo, InGame, Paused }

        public static GameApp I { get; private set; }
        public State Current { get; private set; }

        /// 正在玩（不是暫停、也還沒過關）：只有這時候可以操作鎔金術。
        public bool Playing => Current == State.InGame && !won;


        Camera cam;
        CameraDirector director;
        LevelInfo level;
        Showroom showroom;
        DemoDirector demo;
        MenuUI ui;
        AudioSource musicSource, sfxSource;
        AudioClip clickClip, hoverClip;

        PlayerController player;
        AllomancySession allomancy;
        StoryDirector story;
        float playTime;
        bool won;
        State settingsReturn = State.MainMenu;
        int previewIndex;

        // ------------------------------------------------------------------
        // 啟動
        // ------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameApp>() != null) return;
            new GameObject("GameApp").AddComponent<GameApp>();
        }

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
            Application.targetFrameRate = 120;

            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.farClipPlane = 400f;
            cam.transform.position = new Vector3(0, 20, -45);
            director = cam.gameObject.AddComponent<CameraDirector>();

            EnsureEventSystem();
            SetupAudio();
            SettingsManager.Init(cam);

            level = LevelBuilder.Build(transform);
            showroom = Showroom.Build(transform);
            demo = gameObject.AddComponent<DemoDirector>();
            demo.Finished += EnterMainMenu;
            ui = MenuUI.Create(this);

            Debug.Log($"[FunctionSelection] 關卡由迴圈產生了 {level.objectCount} 組物件（房屋、路燈、樹、岩石、花叢、圍籬、遠山、山壁、橋、城牆、城堡）。");
            EnterMainMenu();
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();    // 滑鼠、鍵盤方向鍵 / Enter、手把都能操作選單
        }

        void SetupAudio()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = AudioSynth.Music();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.Play();
            SettingsManager.Music = musicSource;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            clickClip = AudioSynth.Click();
            hoverClip = AudioSynth.Hover();
        }

        // ------------------------------------------------------------------
        // 狀態切換（由 MenuUI 的按鈕呼叫）
        // ------------------------------------------------------------------

        void SetState(State s)
        {
            Current = s;
            ui.Show(s);
            bool locked = s == State.InGame;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void EnterMainMenu()
        {
            Time.timeScale = 1f;
            demo.End();
            DespawnPlayer();
            director.SetOrbit(Vector3.zero, 42f, 17f, 5f);
            if (!musicSource.isPlaying) { musicSource.UnPause(); if (!musicSource.isPlaying) musicSource.Play(); }   // 看完影片回來恢復音樂
            SetState(State.MainMenu);
        }

        public void EnterSettings()
        {
            settingsReturn = Current == State.Paused ? State.Paused : State.MainMenu;
            SetState(State.Settings);
        }

        public void LeaveSettings()
        {
            SettingsManager.Save();
            if (settingsReturn == State.Paused) SetState(State.Paused);
            else SetState(State.MainMenu);
        }

        public void RebuildSettingsPanel() => ui.RebuildSettings();

        public void EnterCharacterSelect()
        {
            previewIndex = CharacterCatalog.SelectedIndex;
            showroom.Select(previewIndex);
            UpdateCharacterView();
            SetState(State.CharacterSelect);
        }

        public void ChangeCharacter(int delta)
        {
            previewIndex = (previewIndex + delta + CharacterCatalog.All.Length) % CharacterCatalog.All.Length;
            showroom.Select(previewIndex);
            UpdateCharacterView();
        }

        public void ConfirmCharacter()
        {
            CharacterCatalog.SelectedIndex = previewIndex;
            var d = CharacterCatalog.Selected;
            ui.Toast($"{d.name} the {d.title.Replace("The ", "")} joins your adventure!");
            UpdateCharacterView();
            EnterMainMenu();
        }

        void UpdateCharacterView()
        {
            ui.ShowCharacter(CharacterCatalog.All[previewIndex], previewIndex == CharacterCatalog.SelectedIndex);
            showroom.CameraPose(out var pos, out var look);
            director.SetPose(pos, look);
        }

        public void EnterDemo()
        {
            DespawnPlayer();
            SetState(State.Demo);
            demo.Begin(cam, director, level);
            ui.SetDemoCaption(demo.PlayingVideo);
            if (demo.PlayingVideo) musicSource.Pause();
        }

        public void StartGame()
        {
            Time.timeScale = 1f;
            demo.End();
            DespawnPlayer();

            var def = CharacterCatalog.Selected;
            var rig = CharacterBuilder.Build(def, transform, "Player_" + def.name);
            rig.transform.position = level.playerSpawn;
            rig.transform.rotation = Quaternion.LookRotation(Vector3.forward);   // 面向北方：越過噴泉就看到遠處的城牆
            player = rig.gameObject.AddComponent<PlayerController>();
            player.def = def;
            player.rig = rig;
            player.cam = director;
            player.boundsRadius = level.radius;
            director.SetThirdPerson(rig.transform);
            allomancy = AllomancySession.Begin(this, player, director, cam, level);
            story = StoryDirector.Begin(this, player, allomancy, level);
            allomancy.Hud.objectives = story.ObjectiveMarkers;      // 小地圖的金點 = 現在的目標

            playTime = 0f;
            won = false;
            if (!musicSource.isPlaying) musicSource.Play();
            SetState(State.InGame);
        }

        void DespawnPlayer()
        {
            if (story != null) story.End();
            story = null;
            if (allomancy != null) allomancy.End();     // 立刻還原鏡頭、環境光等設定，再刪除
            allomancy = null;
            if (player != null) Destroy(player.gameObject);
            player = null;
        }

        public void Pause()
        {
            Time.timeScale = 0f;
            SetState(State.Paused);
        }

        public void Resume()
        {
            Time.timeScale = 1f;
            SetState(State.InGame);
        }

        public void Quit()
        {
            SettingsManager.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------
        // 遊戲事件
        // ------------------------------------------------------------------

        /// 劇情走完（站上城牆）：暫停遊戲、放開滑鼠、顯示過關畫面。
        public void CompleteLevel()
        {
            if (won) return;
            won = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ui.ShowWin(playTime);
        }

        /// 給 AllomancySession / StoryDirector 用：顯示提示訊息。
        public void Toast(string message, float seconds = 2.5f) => ui.Toast(message, seconds);

        public void PlayClick() { if (sfxSource != null) sfxSource.PlayOneShot(clickClip); }
        public void PlayHover() { if (sfxSource != null) sfxSource.PlayOneShot(hoverClip, 0.6f); }

        // ------------------------------------------------------------------
        // 每幀：鍵盤快捷鍵與 HUD
        // ------------------------------------------------------------------

        void Update()
        {
            var kb = Keyboard.current;
            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;

            switch (Current)
            {
                case State.InGame:
                    if (won) break;
                    playTime += Time.deltaTime;
                    if (esc) Pause();
                    // 點一下遊戲畫面就重新鎖定滑鼠（例如切到別的視窗回來後）
                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    break;
                case State.Paused:
                    if (esc) Resume();
                    break;
                case State.Settings:
                    if (esc) LeaveSettings();
                    break;
                case State.CharacterSelect:
                    if (esc) EnterMainMenu();
                    else if (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) ChangeCharacter(-1);
                    else if (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) ChangeCharacter(1);
                    break;
                case State.Demo:
                    if (esc) EnterMainMenu();
                    break;
            }

            if ((Current == State.InGame || Current == State.Paused) && player != null)
                ui.UpdateHud(player.def, playTime);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }
    }
}
