using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Allomancy
{
    /// 
    /// 遊戲畫面上的 HUD（右下角）：
    ///   - 半圓形小地圖：另一台由上往下拍的攝影機，畫面輸出到 RenderTexture，再用半圓遮罩裁切
    ///     地圖跟著玩家旋轉，所以「上方」永遠是玩家面對的方向
    ///   - 小地圖上方：縮小版的金屬輪盤（存量、燃燒中的外框加粗發光）
    ///   - 輪盤外圍的大圓弧：血量；受傷時閃白並震動，補血時閃綠
    ///   - 小地圖上的標記：敵人（紅）、燒鐵 / 鋼時的金屬（藍）、燒鋅 / 黃銅時的情緒核心（橘）
    /// 
    public class GameHud : MonoBehaviour
    {
        // 版面（單位：參考解析度 1920x1080 的 UI 像素）
        const float MapRadius = 270f;
        const float WheelRadius = 105f;
        static readonly Vector2 MapCenter = new Vector2(-310f, 0f);          // 相對於右下角；圓心在螢幕底邊上
        static readonly Vector2 WheelCenter = new Vector2(-310f, 420f);
        const float HpRadius = 132f, HpWidth = 12f, HpStart = 260f, HpSweep = -160f;   // 從左下往上順時針到左上

        Transform player;
        MetalReserves reserves;
        PlayerHealth health;
        Camera mapCam;
        float baseMapSize = 32f;                 // 小地圖拍攝範圍（半徑，公尺）

        RectTransform cluster, mapRoot;
        RingSectorGraphic hpBar;
        Text hpText;
        Image hitFlash;
        UISparks wheelSparks;
        MetalWheel wheel;
        float shake, flashWhite, flashGreen;

        // 小地圖標記的物件池：每種顏色一組，用多少拿多少
        readonly Dictionary<Color, List<RectTransform>> markerPools = new Dictionary<Color, List<RectTransform>>();
        readonly Dictionary<Color, int> markerUsed = new Dictionary<Color, int>();
        static readonly Color EnemyColor = new Color(1f, 0.25f, 0.25f);
        static readonly Color MetalColor = new Color(0.35f, 0.65f, 1f);
        static readonly Color EmotionColor = new Color(1f, 0.7f, 0.15f);

        public static GameHud Create(Canvas canvas, Transform player, MetalReserves reserves, PlayerHealth health)
        {
            var hud = canvas.gameObject.AddComponent<GameHud>();
            hud.player = player;
            hud.reserves = reserves;
            hud.health = health;
            hud.Build(canvas.transform);
            health.Changed += hud.OnHealthChanged;
            reserves.Toggled += (m, on) =>
            {
                if (on) hud.wheelSparks.Burst(hud.wheel.SectorPoint((int)m), MetalInfo.Colors[(int)m], 16, 150f);
            };
            return hud;
        }

        void Build(Transform canvas)
        {
            // 受傷時整個畫面閃紅
            hitFlash = NewImage("HitFlash", canvas, new Color(0.8f, 0f, 0f, 0f));
            hitFlash.rectTransform.anchorMin = Vector2.zero;
            hitFlash.rectTransform.anchorMax = Vector2.one;
            hitFlash.rectTransform.offsetMin = hitFlash.rectTransform.offsetMax = Vector2.zero;

            // 右下角的群組：固定在螢幕右下角
            cluster = new GameObject("BottomRight", typeof(RectTransform)).GetComponent<RectTransform>();
            cluster.SetParent(canvas, false);
            cluster.anchorMin = cluster.anchorMax = cluster.pivot = new Vector2(1f, 0f);
            cluster.sizeDelta = Vector2.zero;
            cluster.anchoredPosition = Vector2.zero;

            BuildMinimap();
            BuildWheelAndHealth();
            BuildHint(canvas);
        }

        // ------------------------------------------------------------------
        // 小地圖
        // ------------------------------------------------------------------
        void BuildMinimap()
        {
            // 1. 由上往下拍的攝影機，畫面輸出到 512x512 的 RenderTexture
            var rtex = new RenderTexture(512, 512, 16) { name = "MinimapRT" };
            mapCam = new GameObject("MinimapCamera").AddComponent<Camera>();
            mapCam.orthographic = true;
            mapCam.orthographicSize = baseMapSize;
            mapCam.targetTexture = rtex;
            mapCam.clearFlags = CameraClearFlags.SolidColor;
            mapCam.backgroundColor = new Color(0.1f, 0.12f, 0.1f);
            mapCam.nearClipPlane = 1f;
            mapCam.farClipPlane = 200f;

            // 2. 半圓遮罩：上半圓（0° 到 180°），平的那邊貼齊螢幕底邊
            //    Mask 會用這個圖形的形狀裁切底下的子物件；遮罩本身不顯示
            var maskShape = RingSectorGraphic.Create("MapMask", cluster, RingSectorGraphic.Mode.Fill,
                                                     Color.white, 0f, MapRadius, 0f, 180f);
            mapRoot = maskShape.rectTransform;
            mapRoot.anchoredPosition = MapCenter;
            var mask = maskShape.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // 3. 把 RenderTexture 貼在遮罩裡，大小 = 直徑，圓心對準玩家
            var raw = new GameObject("MapImage", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RawImage>();
            raw.transform.SetParent(mapRoot, false);
            raw.texture = rtex;
            raw.rectTransform.sizeDelta = new Vector2(MapRadius * 2f, MapRadius * 2f);
            raw.color = new Color(0.85f, 0.95f, 0.85f);      // 稍微偏綠，像地圖
            raw.raycastTarget = false;

            // 4. 外框與玩家箭頭（不放在遮罩裡，才不會被裁掉）
            var border = RingSectorGraphic.Create("MapBorder", cluster, RingSectorGraphic.Mode.Outline,
                                                  new Color(0.9f, 0.85f, 0.7f, 0.9f), 0f, MapRadius, 0f, 180f);
            border.rectTransform.anchoredPosition = MapCenter;
            border.SetThickness(5f);
            var arrow = RingSectorGraphic.Create("PlayerArrow", cluster, RingSectorGraphic.Mode.Fill,
                                                 Color.white, 0f, 26f, 70f, 40f);   // 70°–110° 的細長扇形 = 朝上的箭頭
            arrow.rectTransform.anchoredPosition = MapCenter;
            var label = NewText(cluster, "MAP", 20);
            label.rectTransform.anchoredPosition = MapCenter + new Vector2(0, MapRadius - 26f);   // 半圓內側頂端
        }

        // ------------------------------------------------------------------
        // 小輪盤 + 血量圓弧
        // ------------------------------------------------------------------
        void BuildWheelAndHealth()
        {
            // 血量圓弧的底（暗紅色，代表最大血量）與實際血量
            var track = RingSectorGraphic.Create("HpTrack", cluster, RingSectorGraphic.Mode.Fill,
                                                 new Color(0.25f, 0.02f, 0.02f, 0.7f), HpRadius - HpWidth / 2, HpRadius + HpWidth / 2, HpStart, HpSweep);
            track.rectTransform.anchoredPosition = WheelCenter;
            hpBar = RingSectorGraphic.Create("Hp", cluster, RingSectorGraphic.Mode.Fill,
                                             new Color(0.9f, 0.12f, 0.12f), HpRadius - HpWidth / 2, HpRadius + HpWidth / 2, HpStart, HpSweep);
            hpBar.rectTransform.anchoredPosition = WheelCenter;

            hpText = NewText(cluster, "", 20);
            hpText.rectTransform.anchoredPosition = WheelCenter + MetalInfo.Direction(180f) * (HpRadius + 32f);   // 圓弧正左邊

            wheel = MetalWheel.Create(cluster, reserves, WheelRadius, large: false);
            wheel.Rect.anchoredPosition = WheelCenter;
            wheelSparks = UISparks.Create(wheel.transform);
        }

        void BuildHint(Transform canvas)
        {
            var t = NewText(canvas, "WASD move · Shift run · Space jump · Mouse look\n" +
                                    "Hold MMB: metal wheel (drag, release to burn / extinguish; LMB while holding: toggle more) · 1–8: quick toggle\n" +
                                    "Hold RMB: aim at metal / emotional core · Wheel up: Steel push / Zinc riot · Wheel down: Iron pull / Brass soothe\n" +
                                    "Copper: hide from Seekers (bronze helmets) · Bronze: see enemies' metals · Tin: night vision, footsteps, smell vials · Pewter: strength\n" +
                                    "H: take damage · J: heal (test keys)", 20);
            t.alignment = TextAnchor.UpperLeft;
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(24f, -20f);
            t.rectTransform.sizeDelta = new Vector2(1300f, 120f);
            t.color = new Color(1, 1, 1, 0.75f);
        }

        void OnHealthChanged(float delta)
        {
            if (delta < 0f)
            {
                shake = 0.35f;
                flashWhite = 1f;
                var c = hitFlash.color;
                c.a = 0.35f;
                hitFlash.color = c;
                SoundFx.Play(SoundFx.Kind.Hit);
            }
            else flashGreen = 1f;
        }

        // ------------------------------------------------------------------
        // 每幀更新
        // ------------------------------------------------------------------
        void LateUpdate()
        {
            if (player == null) return;
            float dt = Time.unscaledDeltaTime;

            // 血量圓弧：長度 = 總角度 × 血量比例
            hpBar.SetShape(hpBar.innerRadius, hpBar.outerRadius, HpStart, HpSweep * health.Fraction);
            flashWhite = Mathf.MoveTowards(flashWhite, 0f, dt * 3f);
            flashGreen = Mathf.MoveTowards(flashGreen, 0f, dt * 2f);
            var hp = new Color(0.9f, 0.12f, 0.12f);
            if (health.Fraction < 0.3f) hp = Color.Lerp(hp, Color.white, 0.4f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f))); // 低血量閃爍
            hp = Color.Lerp(hp, Color.white, flashWhite);
            hp = Color.Lerp(hp, new Color(0.3f, 1f, 0.4f), flashGreen);
            hpBar.color = hp;
            hpText.text = $"{Mathf.CeilToInt(health.current)}";

            // 受傷震動：整個右下角群組隨機位移，越來越小
            shake = Mathf.MoveTowards(shake, 0f, dt);
            cluster.anchoredPosition = Random.insideUnitCircle * 14f * shake;

            var fc = hitFlash.color;
            fc.a = Mathf.MoveTowards(fc.a, 0f, dt * 1.2f);
            hitFlash.color = fc;

            UpdateMinimapCamera();
            UpdateMarkers();
        }

        void UpdateMinimapCamera()
        {
            // 跟著玩家，往下看；繞 Y 軸轉到和玩家同一個方向 → 地圖上方 = 玩家前方
            float yaw = player.eulerAngles.y;
            mapCam.transform.position = player.position + Vector3.up * 80f;
            mapCam.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            // 燃燒錫（增強感官）：地圖看得更遠
            float want = reserves.IsBurning(Metal.Tin) ? baseMapSize * 1.6f : baseMapSize;
            mapCam.orthographicSize = Mathf.Lerp(mapCam.orthographicSize, want, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
        }

        void UpdateMarkers()
        {
            foreach (var k in new List<Color>(markerUsed.Keys)) markerUsed[k] = 0;

            bool seeMetal = reserves.IsBurning(Metal.Iron) || reserves.IsBurning(Metal.Steel);
            bool seeEmotion = reserves.IsBurning(Metal.Zinc) || reserves.IsBurning(Metal.Brass);

            foreach (var e in Enemy.All)
            {
                PlaceMarker(e.transform.position, EnemyColor, 9f);
                if (seeEmotion) PlaceMarker(e.transform.position, EmotionColor, 5f);
            }
            if (seeMetal)
                foreach (var m in MetalObject.All)
                    PlaceMarker(m.CenterOfMass, MetalColor, 4f);

            // 這一幀沒用到的標記藏起來
            foreach (var kv in markerPools)
                for (int i = markerUsed[kv.Key]; i < kv.Value.Count; i++)
                    kv.Value[i].gameObject.SetActive(false);
        }

        /// 
        /// 把世界座標換成小地圖上的位置：
        ///   d = 目標 - 玩家（只看 x、z）
        ///   右 = (cos ψ, -sin ψ)、前 = (sin ψ, cos ψ)，ψ 是玩家的 yaw
        ///   地圖上的 x = d·右，y = d·前，再乘上「每公尺幾個像素」
        /// 
        void PlaceMarker(Vector3 world, Color color, float size)
        {
            var d = world - player.position;
            float yaw = player.eulerAngles.y * Mathf.Deg2Rad;
            var local = new Vector2(d.x * Mathf.Cos(yaw) - d.z * Mathf.Sin(yaw),
                                    d.x * Mathf.Sin(yaw) + d.z * Mathf.Cos(yaw));
            float pixelsPerMeter = MapRadius / mapCam.orthographicSize;
            var p = local * pixelsPerMeter;
            if (p.magnitude > MapRadius - 6f || p.y < 0f) return;     // 超出半圓就不畫

            if (!markerPools.TryGetValue(color, out var pool))
            {
                pool = new List<RectTransform>();
                markerPools[color] = pool;
                markerUsed[color] = 0;
            }
            int used = markerUsed[color];
            if (used == pool.Count)
            {
                var g = RingSectorGraphic.Create("Marker", mapRoot, RingSectorGraphic.Mode.Fill, color, 0f, 1f, 0f, 360f);
                pool.Add(g.rectTransform);
            }
            var rt = pool[used];
            rt.gameObject.SetActive(true);
            rt.anchoredPosition = p;
            rt.GetComponent<RingSectorGraphic>().SetOuterRadius(size);
            markerUsed[color] = used + 1;
        }

        // ------------------------------------------------------------------

        static Image NewImage(string name, Transform parent, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text NewText(Transform parent, string s, int size)
        {
            var t = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = s;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            return t;
        }
    }
}
