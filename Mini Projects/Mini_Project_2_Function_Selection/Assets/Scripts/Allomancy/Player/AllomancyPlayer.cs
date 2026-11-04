using System;
using UnityEngine;

namespace Allomancy
{
    ///
    /// 「會鎔金術的玩家」：掛在村莊選到的角色身上，給鎔金術的各個模組（瞄準、藍線、光暈、紅銅雲）使用。
    ///
    /// 鎔金術模組不認識村莊的程式（FunctionSelection）：
    /// 移動、跳躍、鏡頭都是村莊的 PlayerController / CameraDirector 在做，
    /// 這裡只放鎔金術需要的資料，以及三個由外面接上的「插座」（委派 delegate）：
    ///   groundedCheck   ：玩家現在有沒有踩在地上（滯空推地面時要用）
    ///   impulseHandler  ：鋼推 / 鐵拉把玩家推開時，要怎麼把速度加到玩家身上
    ///   canAct          ：現在能不能操作鎔金術（暫停、過關畫面時不行）
    ///   launchHandler   ：拉有落腳點的固定物時，直接把玩家「發射」出去（設定速度，飛行中不受鍵盤影響）
    /// 由 AllomancySession 在遊戲開始時接好。這樣兩邊的程式互不依賴，各自都能單獨說明。
    ///
    public class AllomancyPlayer : MonoBehaviour
    {
        public MetalReserves reserves;
        public MetalSelector selector;
        public Transform Chest;                     // 鋼線的起點（跟著身體一起上下晃）
        public bool aiming;                         // 由 AllomanticTargeting 設定：按住右鍵瞄準中

        public Func<bool> groundedCheck;
        public Action<Vector3> impulseHandler;
        public Func<bool> canAct;
        public Action<Vector3> launchHandler;
        public float gravity = 22f;                 // 玩家受到的重力（算拋物線用），由 AllomancySession 設定

        public bool IsGrounded => groundedCheck == null || groundedCheck();
        public bool CanAct => canAct == null || canAct();
        public bool Selecting => selector != null && selector.IsOpen;

        /// 被鋼推 / 鐵拉：交給村莊的 PlayerController 處理速度。
        public void AddImpulse(Vector3 v) => impulseHandler?.Invoke(v);

        /// 直接設定玩家的速度，沿拋物線飛出去。
        public void Launch(Vector3 v) => launchHandler?.Invoke(v);
    }
}
