using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Allomancy
{
    public enum TargetKind { None, Metal, Emotion }

    /// 
    /// 瞄準與發動鎔金術：
    ///   按住右鍵 → 鏡頭拉近，選出最靠近畫面中央（準星）的目標：
    ///       燒鐵或鋼時可以瞄準金屬（看得到藍線的那些）
    ///       燒鋅或黃銅時可以瞄準敵人的情緒核心
    ///   滾輪往上 = 上箭頭：金屬 → 鋼推；情緒核心 → 鋅煽動
    ///   滾輪往下 = 下箭頭：金屬 → 鐵拉；情緒核心 → 黃銅安撫
    ///   對應的金屬沒在燒 → 箭頭是灰色（未解鎖），按了只會發出錯誤音
    /// 每次使用消耗該金屬 2% 的存量。
    /// 
    public class AllomanticTargeting : MonoBehaviour
    {
        public PlayerController player;
        public MetalReserves reserves;
        public Camera view;
        public float range = 35f;
        [Tooltip("目標離準星多近才選得到（螢幕高度的比例）")]
        public float pickRadius = 0.12f;

        public bool Aiming { get; private set; }
        public TargetKind Kind { get; private set; }
        public MetalObject TargetMetal { get; private set; }
        public Enemy TargetEnemy { get; private set; }

        /// (是上箭頭嗎, 有沒有成功) — 給 UI 做閃爍。
        public event Action<bool, bool> Used;

        const float PushSpeed = 14f;     // 一次推 / 拉給的速度預算（公尺 / 秒），依重量分給玩家和目標
        const float MetalCost = 0.02f;
        float cooldown;

        public Vector3 TargetPoint => Kind == TargetKind.Metal ? TargetMetal.CenterOfMass
                                    : Kind == TargetKind.Emotion ? TargetEnemy.CorePosition : Vector3.zero;

        /// 上 / 下箭頭各自需要的金屬。
        public Metal UpMetal => Kind == TargetKind.Emotion ? Metal.Zinc : Metal.Steel;
        public Metal DownMetal => Kind == TargetKind.Emotion ? Metal.Brass : Metal.Iron;
        public bool UpUnlocked => reserves.IsBurning(UpMetal);
        public bool DownUnlocked => reserves.IsBurning(DownMetal);
        float defaultFixedDelta;
        Renderer[] highlighted;          // 目前被高亮的 Renderer
        Material highlightMat;

        void Awake()
        {
            defaultFixedDelta = Time.fixedDeltaTime;
            highlightMat = Build.GlowTransparent(new Color(1f, 1f, 1f, 0.35f));   // 再畫一次物體、疊一層 35% 白色 = 變亮
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Aiming = mouse.rightButton.isPressed && (player.selector == null || !player.selector.IsOpen);
            player.aiming = Aiming;

            // 瞄準時慢動作 50%；輪盤開著時時間由輪盤控制（0.25），這裡不插手
            bool selecting = player.selector != null && player.selector.IsOpen;
            if (!selecting)
            {
                float want = Aiming ? 0.5f : 1f;
                if (!Mathf.Approximately(Time.timeScale, want))
                {
                    Time.timeScale = want;
                    Time.fixedDeltaTime = defaultFixedDelta * want;   // 物理也一起變慢，才不會卡頓
                }
            }

            if (!Aiming)
            {
                Kind = TargetKind.None;
                SetHighlight(null);
                return;
            }

            PickTarget();
            SetHighlight(Kind == TargetKind.Metal ? TargetMetal.Renderers
                    : Kind == TargetKind.Emotion ? TargetEnemy.Renderers : null);

            // 滾輪：每一格觸發一次；冷卻 0.12 秒，避免一格滾輪被算成好幾次
            cooldown -= Time.unscaledDeltaTime;
            float scroll = mouse.scroll.ReadValue().y;
            if (Kind != TargetKind.None && cooldown <= 0f && Mathf.Abs(scroll) > 0.01f)
            {
                Act(scroll > 0f);
                cooldown = 0.12f;
            }
        }

        /// 
        /// 在所有可瞄準的東西裡，找螢幕上離畫面中央最近的一個。
        /// WorldToScreenPoint 把 3D 位置換成螢幕像素；z ≤ 0 代表在鏡頭後面。
        /// 
        void PickTarget()
        {
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float best = pickRadius * Screen.height;
            Kind = TargetKind.None;
            TargetMetal = null;
            TargetEnemy = null;

            if (reserves.IsBurning(Metal.Iron) || reserves.IsBurning(Metal.Steel))
            {
                foreach (var m in MetalObject.All)
                {
                    var p = m.CenterOfMass;
                    if (Vector3.Distance(player.Chest.position, p) > range) continue;
                    var sp = view.WorldToScreenPoint(p);
                    if (sp.z <= 0f) continue;
                    float d = Vector2.Distance(new Vector2(sp.x, sp.y), center);
                    if (d < best) { best = d; Kind = TargetKind.Metal; TargetMetal = m; TargetEnemy = null; }
                }
            }

            if (reserves.IsBurning(Metal.Zinc) || reserves.IsBurning(Metal.Brass))
            {
                foreach (var e in Enemy.All)
                {
                    var p = e.CorePosition;
                    if (Vector3.Distance(player.Chest.position, p) > range) continue;
                    var sp = view.WorldToScreenPoint(p);
                    if (sp.z <= 0f) continue;
                    float d = Vector2.Distance(new Vector2(sp.x, sp.y), center);
                    if (d < best) { best = d; Kind = TargetKind.Emotion; TargetEnemy = e; TargetMetal = null; }
                }
            }
        }

        void Act(bool up)
        {
            var metal = up ? UpMetal : DownMetal;
            if (!reserves.IsBurning(metal))                       // 未解鎖
            {
                SoundFx.Play(SoundFx.Kind.Empty);
                Used?.Invoke(up, false);
                return;
            }
            reserves.amount[(int)metal] = Mathf.Max(0f, reserves.amount[(int)metal] - MetalCost);

            if (Kind == TargetKind.Metal)
            {
                PushPull(TargetMetal, up ? 1f : -1f);
                SoundFx.Play(up ? SoundFx.Kind.Push : SoundFx.Kind.Pull);
            }
            else
            {
                if (up) TargetEnemy.Riot();
                else TargetEnemy.Soothe();
                SoundFx.Play(up ? SoundFx.Kind.Riot : SoundFx.Kind.Soothe);
            }
            Used?.Invoke(up, true);
        }

        void SetHighlight(Renderer[] rs)
        {
            if (highlighted == rs) return;           // 同一個目標，不用重做
            ClearHighlight();
            highlighted = rs;
            if (rs == null) return;
            foreach (var r in rs)
            {
                var mats = r.sharedMaterials;
                Array.Resize(ref mats, mats.Length + 1);
                mats[^1] = highlightMat;
                r.sharedMaterials = mats;
            }
        }

        void ClearHighlight()
        {
            if (highlighted == null) return;
            foreach (var r in highlighted)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats.Length > 1 && mats[^1] == highlightMat)
                {
                    Array.Resize(ref mats, mats.Length - 1);
                    r.sharedMaterials = mats;
                }
            }
            highlighted = null;
        }

        /// 
        /// 鋼推（sign = +1）/ 鐵拉（sign = -1）。
        /// 作用力相等、方向相反：輕的那一方動得多。速度預算依重量比例分配：
        ///   玩家分到 目標重 / (目標重 + 玩家重)，目標分到 玩家重 / (目標重 + 玩家重)
        /// 例：推 2000 公斤的雕像 → 玩家分到 97%，自己被推飛；推 0.3 公斤的錢幣 → 錢幣分到 99.6%，錢幣飛走。
        /// 
        void PushPull(MetalObject target, float sign)
        {
            var dir = (target.CenterOfMass - player.Chest.position).normalized;   // 玩家 → 目標
            float P = MetalObject.PlayerMass;
            float m = target.MovingMass;

            // 滯空時「推」腳下地上的東西：東西被地面頂住，視為無限重 → 只有玩家動，往上飛
            bool groundAnchor = sign > 0f && !player.IsGrounded && dir.y < 0f && IsOnGround(target);
            bool infinite = target.Anchored || groundAnchor;

            float playerShare = infinite ? 1f : m / (m + P);
            float targetShare = infinite ? 0f : P / (m + P);

            player.AddImpulse(-dir * sign * PushSpeed * playerShare);
            if (targetShare <= 0f) return;

            var v = dir * sign * PushSpeed * targetShare;
            if (target.body != null && !target.body.isKinematic)
            {
                // 沿著藍線方向飛出去：直接「設定」速度（不是累加），之後交給重力畫出拋物線
                target.body.useGravity = true;
                target.body.linearVelocity = v;
                target.body.WakeUp();
            }
            else if (target.owner != null)
            {
                target.owner.Knockback(v);
            }
        }

        /// 
        /// 物體是不是放在地上：從它的中心往下打一條射線，長度 = 半高 + 0.3 公尺，打到東西就算。
        /// 射線從物體內部出發時不會打到物體自己的碰撞體，所以打到的一定是地面或底下的東西。
        /// 
        static bool IsOnGround(MetalObject t)
        {
            var b = t.WorldBounds;
            return Physics.Raycast(b.center, Vector3.down, b.extents.y + 0.3f, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
