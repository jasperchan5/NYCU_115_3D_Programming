using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Allomancy
{
    /// 
    /// 進入點：按下 Play 後自動把所有東西組起來（場景裡不需要手動擺物件）。
    ///   1. 場景（Arena）  2. 玩家與金屬存量、血量  3. 敵人與藥瓶
    ///   4. UI：HUD（小地圖、小輪盤、血量）與金屬選擇輪盤
    ///   5. 燒鐵 / 鋼的藍線、右鍵瞄準與推 / 拉 / 煽動 / 安撫
    /// 也負責：燃燒金屬時在玩家胸口噴出彩色粒子、測試用按鍵（H 受傷、J 補血）、死亡後重生。
    /// 
    public class GameBootstrap : MonoBehaviour
    {
        static readonly Vector3 Spawn = new Vector3(0f, 0.2f, -8f);

        PlayerController player;
        PlayerHealth health;
        MetalReserves reserves;
        ParticleSystem flare;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            new GameObject("AllomancyGame").AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.farClipPlane = 300f;
            SoundFx.Create(transform);

            // 1. 場景
            var vialPoints = Arena.Build(transform, out var enemyHomes);

            // 2. 玩家
            player = PlayerController.Create(transform, Spawn, cam);
            reserves = player.gameObject.AddComponent<MetalReserves>();
            health = player.gameObject.AddComponent<PlayerHealth>();
            health.reserves = reserves;
            player.reserves = reserves;
            health.Died += () =>
            {
                player.Teleport(Spawn);
                health.Heal(health.max);
            };

            // 3. 敵人與藥瓶
            for (int i = 0; i < enemyHomes.Count; i++)
            {
                var e = Enemy.Create(transform, enemyHomes[i], 4f, i * 1.7f);
                e.player = player.transform;
                e.playerHealth = health;
                e.playerReserves = reserves;
                EnemyAllomancy.Setup(e, reserves, i);
            }
            foreach (var p in vialPoints)
            {
                var v = MetalVial.Create(transform, p);
                v.player = player.transform;
                v.reserves = reserves;
                ScentTrail.Attach(v, reserves, player.transform);
            }

            // 4. 瞄準（右鍵）
            var targeting = player.gameObject.AddComponent<AllomanticTargeting>();
            targeting.player = player;
            targeting.reserves = reserves;
            targeting.view = cam;

            // 5. UI：建立順序 = 疊放順序（後建立的在上面）
            var canvas = CreateCanvas();
            GameHud.Create(canvas, player.transform, reserves, health);
            BronzeSight.Create(canvas, reserves, cam);
            TinSenses.Create(canvas, player.transform, reserves, cam);
            TargetUI.Create(canvas, targeting, cam);
            player.selector = MetalSelector.Create(canvas.transform, reserves);   // 輪盤最後建立 → 疊在最上面

            // 6. 鋼線
            var sight = player.gameObject.AddComponent<SteelSight>();
            sight.reserves = reserves;
            sight.chest = player.Chest;
            sight.view = cam;
            sight.targeting = targeting;

            PewterAura.Create(player, reserves);
            CopperCloud.Create(player, reserves);
            flare = CreateFlare(player.Chest);
            reserves.Toggled += (m, on) =>
            {
                if (!on) return;
                var main = flare.main;
                main.startColor = MetalInfo.Colors[(int)m];
                flare.Emit(40);
            };

            Cursor.lockState = CursorLockMode.Locked;
        }

        static Canvas CreateCanvas()
        {
            var go = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;          // 以高度為準，小地圖在各種寬度下大小一致
            return canvas;
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
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(shader);
            return ps;
        }

        void Update()
        {
            // 測試按鍵：示範血量圓弧的受傷 / 補血效果
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.hKey.wasPressedThisFrame) health.Damage(15f);
            if (kb.jKey.wasPressedThisFrame) health.Heal(20f);
        }
    }
}
