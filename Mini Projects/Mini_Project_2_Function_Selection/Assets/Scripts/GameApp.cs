using System.Collections.Generic;
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
    public class GameApp : MonoBehaviour
    {
        public enum State { MainMenu, Settings, CharacterSelect, Demo, InGame, Paused }

        public static GameApp I { get; private set; }
        public State Current { get; private set; }

        /// 只有在遊戲中才回傳玩家（怪物追人、撿寶石都靠它判斷）。
        public PlayerController ActivePlayer => Current == State.InGame ? player : null;

        const int MaxHp = 5;

        Camera cam;
        CameraDirector director;
        LevelInfo level;
        Showroom showroom;
        DemoDirector demo;
        MenuUI ui;
        AudioSource musicSource, sfxSource;
        AudioClip clickClip, hoverClip, collectClip, hurtClip;

        PlayerController player;
        Transform gemRoot;
        readonly List<Gem> gems = new List<Gem>();
        int gemsCollected, hp;
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
            SpawnMonsters();
            showroom = Showroom.Build(transform);
            demo = gameObject.AddComponent<DemoDirector>();
            demo.Finished += EnterMainMenu;
            ui = MenuUI.Create(this);

            Debug.Log($"[FunctionSelection] 關卡由迴圈產生了 {level.objectCount} 組物件（房屋、路燈、樹、岩石、花叢、圍籬、遠山）。");
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
            collectClip = AudioSynth.Collect();
            hurtClip = AudioSynth.Hurt();
        }

        void SpawnMonsters()
        {
            var root = new GameObject("Monsters").transform;
            root.SetParent(transform, false);
            var rng = new System.Random(7);
            Color[] colors =
            {
                new Color(0.45f, 0.85f, 0.4f), new Color(0.65f, 0.4f, 0.9f), new Color(0.95f, 0.45f, 0.4f),
                new Color(0.35f, 0.7f, 0.95f), new Color(0.95f, 0.75f, 0.3f), new Color(0.9f, 0.5f, 0.8f),
            };
            for (int i = 0; i < level.monsterHomes.Count; i++)
                Monster.Build(root, level.monsterHomes[i], colors[i % colors.Length], rng);
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
            rig.transform.rotation = Quaternion.LookRotation(Vector3.back);   // 背對噴泉、面向村莊南邊
            player = rig.gameObject.AddComponent<PlayerController>();
            player.def = def;
            player.rig = rig;
            player.cam = director;
            player.boundsRadius = level.radius;
            director.SetThirdPerson(rig.transform);

            SpawnGems();
            hp = MaxHp;
            playTime = 0f;
            won = false;
            if (!musicSource.isPlaying) musicSource.Play();
            SetState(State.InGame);
            ui.Toast($"Find all {gems.Count} gems — watch out for the slimes!", 4f);
        }

        void SpawnGems()
        {
            if (gemRoot != null) Destroy(gemRoot.gameObject);
            gems.Clear();
            gemsCollected = 0;
            gemRoot = new GameObject("Gems").transform;
            gemRoot.SetParent(transform, false);
            for (int i = 0; i < level.gemPoints.Count; i++)
            {
                var c = Color.HSVToRGB(i / (float)level.gemPoints.Count, 0.6f, 1f);
                gems.Add(Gem.Build(gemRoot, level.gemPoints[i], c, i * 0.7f));
            }
        }

        void DespawnPlayer()
        {
            if (player != null) Destroy(player.gameObject);
            player = null;
            if (gemRoot != null) Destroy(gemRoot.gameObject);
            gemRoot = null;
            gems.Clear();
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

        public void OnGemCollected(Gem g)
        {
            if (!gems.Remove(g)) return;
            gemsCollected++;
            Burst.At(g.transform.position, new Color(1f, 0.9f, 0.4f));
            Destroy(g.gameObject);
            sfxSource.PlayOneShot(collectClip);
            if (gems.Count == 0)
            {
                won = true;
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ui.ShowWin(playTime, hp);
            }
            else ui.Toast($"Gem!  {gems.Count} left", 1.2f);
        }

        public void OnPlayerHit(Monster m)
        {
            if (player == null) return;
            hp--;
            sfxSource.PlayOneShot(hurtClip);
            Burst.At(player.transform.position + Vector3.up, new Color(1f, 0.3f, 0.3f), 25, 3f);
            player.Knockback(m.transform.position, 9f);
            if (hp <= 0)
            {
                hp = MaxHp;
                player.Teleport(level.playerSpawn);
                ui.Toast("You fainted!  Back to the plaza.", 3f);
            }
            else ui.Toast("Ouch!", 0.8f);
        }

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
                ui.UpdateHud(player.def, hp, MaxHp, gemsCollected, gemsCollected + gems.Count, playTime);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }
    }
}
