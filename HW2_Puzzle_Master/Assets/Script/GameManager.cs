using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleMaster
{
    /// 遊戲的四個狀態：開場 → 遊玩中 → 勝利 / 失敗。
    public enum GameState { Intro, Playing, Won, Lost }

    /// 
    /// 遊戲狀態管理（整個遊戲只有一個，用 Instance 取得）。
    ///   - 按下 Play 後自動建立，不用在場景裡手動擺
    ///   - 開場畫面時 Time.timeScale = 0：世界暫停，怪物不會動、計時器不會跑
    ///   - 開場按 SPACE → 開始遊戲；任何時候按 ESC → 離開遊戲
    ///   - 其他系統（計時器、背景音樂、小地圖…）訂閱 StateChanged，狀態改變時自己做對應的事
    /// 
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public event Action<GameState> StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameManager>() != null) return;   // 場景裡已經有了就不重複建立
            new GameObject("GameManager").AddComponent<GameManager>();
        }

        void Awake()
        {
            Instance = this;
            IntroScreen.Create(this);
            SetState(GameState.Intro);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame) Quit();
            if (State == GameState.Intro && kb.spaceKey.wasPressedThisFrame) SetState(GameState.Playing);
        }

        public void SetState(GameState next)
        {
            State = next;
            // 只有遊玩中時間才會走；開場、勝利、失敗畫面時整個世界暫停
            Time.timeScale = next == GameState.Playing ? 1f : 0f;
            // 遊玩中鎖定滑鼠（用滑鼠轉視角），其他畫面放開滑鼠
            Cursor.lockState = next == GameState.Playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = next != GameState.Playing;
            StateChanged?.Invoke(next);
        }

        /// 離開遊戲。在編輯器裡 Application.Quit() 沒有作用，改成停止 Play 模式。
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}