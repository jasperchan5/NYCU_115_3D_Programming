using System.Collections.Generic;
using Allomancy;
using UnityEngine;
using UnityEngine.UI;

namespace FunctionSelection
{
    ///
    /// 把鎔金術（mini 1 的 Game UI）接進村莊關卡。
    ///   按下 Start Game：GameApp 呼叫 Begin()，在選到的角色身上加上金屬存量、血量、瞄準、藍線、光暈，
    ///                    村莊裡加上金屬物件、士兵、藥瓶，畫面加上鎔金術的 HUD。
    ///   回主選單 / 重玩：GameApp 刪掉這個物件，OnDestroy() 把村莊、鏡頭、時間設定全部還原。
    ///
    /// 兩邊的分工：
    ///   村莊（FunctionSelection）：移動、跳躍、鏡頭、選單、暫停、寶石、史萊姆
    ///   鎔金術（Allomancy）       ：金屬輪盤、HUD、瞄準與推拉、各種金屬的效果、士兵
    /// 這個類別是兩邊唯一的接點，所以鎔金術的程式完全不需要知道村莊的存在。
    ///
    public class AllomancySession : MonoBehaviour
    {
        public MetalReserves Reserves { get; private set; }
        public PlayerHealth Health { get; private set; }

        GameApp app;
        PlayerController mover;
        CameraDirector director;
        AllomancyPlayer allomancer;
        Canvas canvas;
        VillageMetals.Layout layout;
        ParticleSystem flare;
        TinSenses tin;
        Vector3 spawn;
        float defaultFixedDelta;
        bool ended;

        public static AllomancySession Begin(GameApp app, PlayerController mover, CameraDirector director, Camera cam, LevelInfo level)
        {
            var s = new GameObject("AllomancySession").AddComponent<AllomancySession>();
            s.transform.SetParent(app.transform, false);
            s.app = app;
            s.mover = mover;
            s.director = director;
            s.spawn = level.playerSpawn;
            s.defaultFixedDelta = Time.fixedDeltaTime;
            SoundFx.Create(s.transform);

            // 1. 玩家：金屬存量、血量，以及給鎔金術模組用的 AllomancyPlayer
            var player = mover.gameObject;
            s.Reserves = player.AddComponent<MetalReserves>();
            s.Health = player.AddComponent<PlayerHealth>();
            s.Health.reserves = s.Reserves;
            s.Health.Died += s.OnDied;

            s.allomancer = player.AddComponent<AllomancyPlayer>();
            s.allomancer.reserves = s.Reserves;
            s.allomancer.Chest = CreateChest(mover.rig);
            s.allomancer.groundedCheck = () => mover.IsGrounded;     // 接上三個插座
            s.allomancer.impulseHandler = mover.AddImpulse;
            s.allomancer.canAct = () => app.Playing;

            MetalObject.PlayerMass = 70f * mover.def.bulk;            // 越壯的角色越重：Lerk 94.5、Dean 63、Haze 73.5 公斤
            director.scrollZoom = false;                              // 滾輪改成推 / 拉

            // 2. 村莊裡的金屬、士兵（4 種各一個）、金屬藥瓶
            s.layout = VillageMetals.Build(s.transform, level.root);
            for (int i = 0; i < s.layout.soldierHomes.Count; i++)
            {
                var e = Enemy.Create(s.transform, s.layout.soldierHomes[i], 4f, i * 1.7f);
                e.player = player.transform;
                e.playerHealth = s.Health;
                e.playerReserves = s.Reserves;
                EnemyAllomancy.Setup(e, s.Reserves, i);              // 0 搜尋者 / 1 白鑞 / 2 紅銅 / 3 錫
            }
            foreach (var p in s.layout.vialPoints)
            {
                var v = MetalVial.Create(s.transform, p);
                v.player = player.transform;
                v.reserves = s.Reserves;
                ScentTrail.Attach(v, s.Reserves, player.transform);
            }

            // 3. 瞄準（右鍵）
            var targeting = player.AddComponent<AllomanticTargeting>();
            targeting.player = s.allomancer;
            targeting.reserves = s.Reserves;
            targeting.view = cam;

            // 4. UI：建立順序 = 疊放順序（後建立的在上面）
            s.canvas = CreateCanvas(s.transform);
            var hud = GameHud.Create(s.canvas, player.transform, s.Reserves, s.Health);
            hud.objectives = app.GemPositions;                        // 小地圖：寶石（金點）
            BronzeSight.Create(s.canvas, s.Reserves, cam);
            s.tin = TinSenses.Create(s.canvas, player.transform, s.Reserves, cam);
            TargetUI.Create(s.canvas, targeting, cam);
            s.allomancer.selector = MetalSelector.Create(s.canvas.transform, s.Reserves);   // 輪盤最後建立 → 疊在最上面
            s.allomancer.selector.canAct = s.allomancer.canAct;

            // 5. 玩家身上的效果（白鑞要先建：它會複製玩家模型，不能把紅銅雲也包進去）
            var sight = player.AddComponent<SteelSight>();
            sight.reserves = s.Reserves;
            sight.chest = s.allomancer.Chest;
            sight.view = cam;
            sight.targeting = targeting;
            PewterAura.Create(s.allomancer, s.Reserves);
            CopperCloud.Create(s.allomancer, s.Reserves);

            s.flare = CreateFlare(s.allomancer.Chest);
            s.Reserves.Toggled += s.OnToggled;
            return s;
        }

        /// 胸口：鋼線的起點。掛在角色的身體（torso）上，走路時會跟著身體上下晃。
        static Transform CreateChest(CharacterRig rig)
        {
            var chest = new GameObject("Chest").transform;
            chest.SetParent(rig.torso, false);
            chest.localPosition = new Vector3(0f, 0.5f, 0.22f * rig.def.bulk);
            return chest;
        }

        /// 鎔金術自己的 Canvas：排序 5，比村莊選單（10）低 → 暫停、過關畫面會蓋在 HUD 上面。
        /// 沒有 GraphicRaycaster：HUD 不接收滑鼠點擊，不會擋到選單按鈕。
        static Canvas CreateCanvas(Transform parent)
        {
            var go = new GameObject("AllomancyHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 5;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;          // 以高度為準，小地圖在各種寬度下大小一致
            return c;
        }

        /// 每幀把鎔金術的狀態轉給村莊的鏡頭與移動。
        void Update()
        {
            // 設定畫面時把 HUD 藏起來；遊戲中、暫停時顯示
            canvas.enabled = app.Current == GameApp.State.InGame || app.Current == GameApp.State.Paused;

            director.lookLocked = allomancer.Selecting;               // 開著輪盤：滑鼠拿來選金屬，不轉鏡頭
            director.aiming = allomancer.aiming;                      // 瞄準：鏡頭拉近、越肩
            mover.faceCamera = allomancer.aiming;                     // 瞄準：角色轉向鏡頭前方
            mover.speedMultiplier = Reserves.IsBurning(Metal.Pewter) ? 1.4f : 1f;   // 白鑞：跑得更快
        }

        /// 開始燒某種金屬：胸口噴出那種金屬顏色的粒子。
        void OnToggled(Metal m, bool burning)
        {
            if (!burning) return;
            var main = flare.main;
            main.startColor = MetalInfo.Colors[(int)m];
            flare.Emit(40);
        }

        /// 血量歸零：回到廣場、補滿血。
        void OnDied()
        {
            mover.Teleport(spawn);
            Health.Heal(Health.max);
            app.Toast("You fainted!  Back to the plaza.", 3f);
        }

        /// 
        /// 結束遊戲：把借來的東西還回去，然後刪掉自己。
        /// 玩家身上的元件會跟著玩家一起被 GameApp 刪掉；這裡的子物件（士兵、藥瓶、木桶、錢幣、HUD）跟著這個物件刪掉。
        ///
        /// 為什麼要 GameApp 主動呼叫 End()，而不是全部寫在 OnDestroy？
        /// Destroy() 要等到這一幀結束才真的刪除。按「Play Again」時，舊的 session 在同一幀被刪、新的被建立，
        /// 如果還原寫在 OnDestroy，舊的會在新的設定好之後才執行，把新的設定（鏡頭、玩家重量、環境光）蓋掉。
        /// 
        public void End()
        {
            if (ended) return;
            ended = true;
            enabled = false;                                           // 這一幀剩下的時間不要再改鏡頭
            Restore();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!ended) Restore();                                     // 直接關掉遊戲時的保險
        }

        void Restore()
        {
            if (tin != null) tin.RestoreScene();
            if (director != null)
            {
                director.lookLocked = false;
                director.aiming = false;
                director.scrollZoom = true;
            }
            Time.fixedDeltaTime = defaultFixedDelta;
            MetalObject.PlayerMass = 70f;
            if (layout != null)
                foreach (var m in layout.attachedToLevel)
                    if (m != null) Destroy(m);                        // 路燈恢復成普通路燈
        }

        /// 燃燒金屬時從胸口噴出的粒子（顏色 = 金屬的顏色）。
        static ParticleSystem CreateFlare(Transform chest)
        {
            var go = new GameObject("BurnFlare");
            go.transform.SetParent(chest, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 3f;
            main.startSize = 0.08f;
            main.gravityModifier = -0.3f;                 // 往上飄
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;                    // 只在 Emit() 時噴
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MaterialLib.Particle(Color.white);
            return ps;
        }
    }
}
