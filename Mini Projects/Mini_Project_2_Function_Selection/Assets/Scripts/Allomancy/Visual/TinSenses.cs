using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Allomancy
{
    /// 
    /// 燃燒錫：感官增強。
    ///   1. 夜視：環境光變亮、加一盞不投影子的補光、霧變淡 → 陰影裡、山洞裡也看得清楚（淡入淡出）
    ///        開始時記住場景原本的環境光與霧，結束遊戲時（RestoreScene）原封不動還回去
    ///   2. 腳步聲：附近敵人每踩一步，就在螢幕左 / 右邊緣冒出一組往內擴散的聲波弧線
    ///        敵人在你右邊 → 右邊緣；左邊 → 左邊緣
    ///        高度：在你前方 → 偏上；在你背後 → 偏下（用 cos(角度) 決定）
    ///        越近 → 越不透明
    ///   3. 嗅覺：地上的金屬瓶冒出彎彎曲曲的氣味（ScentTrail.cs）
    /// 
    public class TinSenses : MonoBehaviour
    {
        MetalReserves reserves;
        Transform player;
        Camera view;
        RectTransform canvasRect;
        SphericalHarmonicsL2 baseProbe;     // 場景原本的環境光（球諧函數）
        float baseFogDensity, baseFogStart, baseFogEnd;
        float fade, appliedFade;            // appliedFade：上次套用到場景的 fade；沒變就不動場景（沒燒錫時完全不碰）
        Light fill;                         // 夜視用的補光：不投影子的平行光

        const float HearRange = 28f;        // 聽得到腳步的距離
        const float WaveTime = 0.7f;        // 一組聲波從出現到消失的秒數
        const int ArcsPerWave = 3;
        static readonly Color WaveColor = new Color(0.85f, 0.9f, 1f);

        /// 一組聲波（三條同心弧）。用物件池重複使用。
        class Wave
        {
            public RectTransform root;
            public RingSectorGraphic[] arcs = new RingSectorGraphic[ArcsPerWave];
            public float age = WaveTime;            // age ≥ WaveTime 代表閒置
            public float strength;
            public bool right;
        }
        readonly List<Wave> waves = new List<Wave>();

        public static TinSenses Create(Canvas canvas, Transform player, MetalReserves reserves, Camera view)
        {
            var t = new GameObject("TinSenses", typeof(RectTransform)).AddComponent<TinSenses>();
            t.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            t.canvasRect = (RectTransform)canvas.transform;
            t.player = player;
            t.reserves = reserves;
            t.view = view;
            // 記住場景原本的亮度（村莊的 LevelBuilder 已經設定好了）
            t.baseProbe = RenderSettings.ambientProbe;
            t.baseFogDensity = RenderSettings.fogDensity;
            t.baseFogStart = RenderSettings.fogStartDistance;
            t.baseFogEnd = RenderSettings.fogEndDistance;

            // 補光：從上方斜照、不投影子 → 高牆的陰影裡也會被照亮
            t.fill = new GameObject("TinFillLight").AddComponent<Light>();
            t.fill.type = LightType.Directional;
            t.fill.shadows = LightShadows.None;
            t.fill.color = new Color(0.75f, 0.85f, 1f);              // 偏冷的月光色
            t.fill.intensity = 0f;
            t.fill.enabled = false;                                   // 沒燒錫時關掉，免得 URP 把它當成主光源
            t.fill.transform.rotation = Quaternion.Euler(70f, 30f, 0f);
            return t;
        }

        void OnEnable() => Enemy.Stepped += OnStep;
        void OnDisable() => Enemy.Stepped -= OnStep;

        // ------------------------------------------------------------------
        // 1. 夜視
        // ------------------------------------------------------------------
        void Update()
        {
            fade = Mathf.MoveTowards(fade, reserves.IsBurning(Metal.Tin) ? 1f : 0f, Time.unscaledDeltaTime * 1.5f);
            if (!Mathf.Approximately(fade, appliedFade)) ApplyVision(fade);

            UpdateWaves(Time.unscaledDeltaTime);
        }

        /// 
        /// 把夜視套用到場景（fade = 0 時剛好等於原本的樣子）：
        ///   環境光：URP 的物體讀的是 ambientProbe（球諧函數）。在原本的 probe 上「加」一層各方向一樣的冷色光，
        ///           原本天空 / 地面的顏色差異（村莊用的是三色環境光）會保留下來
        ///   補光  ：不投影子的平行光 0 → 0.8，牆和房子的陰影裡也被照亮
        ///   霧    ：指數霧的濃度降到 35%；線性霧（村莊）的起訖距離拉遠 2 倍
        /// 
        void ApplyVision(float k)
        {
            var probe = baseProbe;
            probe.AddAmbientLight(new Color(0.35f, 0.4f, 0.5f) * k);
            RenderSettings.ambientProbe = probe;

            fill.intensity = 0.8f * k;
            fill.enabled = k > 0.001f;

            RenderSettings.fogDensity = Mathf.Lerp(baseFogDensity, baseFogDensity * 0.35f, k);
            RenderSettings.fogStartDistance = Mathf.Lerp(baseFogStart, baseFogStart * 2f, k);
            RenderSettings.fogEndDistance = Mathf.Lerp(baseFogEnd, baseFogEnd * 2f, k);
            appliedFade = k;
        }

        /// 
        /// 遊戲結束（回主選單、重玩）時：環境光、霧還原，補光刪掉。
        /// 要「立刻」還原（由 AllomancySession.End 呼叫），不能等到物件被刪掉：
        /// 重玩時新的 TinSenses 會在同一幀記住場景亮度，那時候必須已經是原本的樣子。
        /// 可以重複呼叫，第二次起什麼都不做。
        /// 
        public void RestoreScene()
        {
            if (appliedFade > 0f)
            {
                RenderSettings.ambientProbe = baseProbe;
                RenderSettings.fogDensity = baseFogDensity;
                RenderSettings.fogStartDistance = baseFogStart;
                RenderSettings.fogEndDistance = baseFogEnd;
                appliedFade = 0f;
            }
            fade = 0f;
            if (fill != null) Destroy(fill.gameObject);
            fill = null;
            enabled = false;
        }

        void OnDestroy() => RestoreScene();

        // ------------------------------------------------------------------
        // 2. 腳步聲
        // ------------------------------------------------------------------
        void OnStep(Enemy e)
        {
            if (!reserves.IsBurning(Metal.Tin)) return;
            var to = e.transform.position - player.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > HearRange) return;

            // 以「鏡頭朝向」為基準的水平角度：0 = 正前方，+90 = 右邊，-90 = 左邊，±180 = 正後方
            var forward = view.transform.forward;
            forward.y = 0f;
            float angle = Vector3.SignedAngle(forward, to, Vector3.up);

            var w = GetWave();
            w.age = 0f;
            w.right = angle >= 0f;
            w.strength = 1f - dist / HearRange;

            float halfW = canvasRect.rect.width / 2f, halfH = canvasRect.rect.height / 2f;
            float y = Mathf.Cos(angle * Mathf.Deg2Rad) * halfH * 0.6f;     // 前方偏上、後方偏下
            w.root.anchoredPosition = new Vector2(w.right ? halfW : -halfW, y);
        }

        Wave GetWave()
        {
            foreach (var w in waves) if (w.age >= WaveTime) return w;
            var nw = new Wave();
            nw.root = new GameObject("FootstepWave", typeof(RectTransform)).GetComponent<RectTransform>();
            nw.root.SetParent(transform, false);
            for (int i = 0; i < ArcsPerWave; i++)
            {
                nw.arcs[i] = RingSectorGraphic.Create("Arc", nw.root, RingSectorGraphic.Mode.Outline, WaveColor, 0f, 20f, 0f, 60f);
                nw.arcs[i].SetThickness(4f);
            }
            waves.Add(nw);
            return nw;
        }

        void UpdateWaves(float dt)
        {
            foreach (var w in waves)
            {
                w.age += dt;
                bool alive = w.age < WaveTime;
                if (w.root.gameObject.activeSelf != alive) w.root.gameObject.SetActive(alive);
                if (!alive) continue;

                float t = w.age / WaveTime;                        // 0 → 1
                // 弧的開口朝螢幕內側：右邊緣的弧以 180°（往左）為中心，左邊緣以 0°（往右）為中心，各張開 ±35°
                float center = w.right ? 180f : 0f;
                for (int i = 0; i < ArcsPerWave; i++)
                {
                    float r = 30f + 28f * i + 70f * t;             // 一層比一層大，而且一起往外擴散
                    w.arcs[i].SetShape(0f, r, center + 35f, -70f);
                    var c = WaveColor;
                    c.a = w.strength * (1f - t) * (1f - 0.25f * i);   // 越外圈越淡，整組隨時間淡出
                    w.arcs[i].color = c;
                }
            }
        }
    }
}