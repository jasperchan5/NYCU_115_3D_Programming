using UnityEngine;
using UnityEngine.UI;

namespace Allomancy
{
    /// 
    /// 金屬輪盤：中央是「胃」，外面一圈 8 個扇形，每個扇形由內往外填色表示存量。
    /// 同一個元件用在兩個地方：
    ///   - 大輪盤（按住右鍵叫出來選金屬）：有文字標籤、滑鼠指到的扇形會往外凸出
    ///   - 小輪盤（右下角小地圖上方）：只顯示存量與燃燒狀態
    /// 燃燒中的扇形：外框變粗、換成金屬的顏色，外面加一圈會呼吸閃爍的光暈。
    /// 
    public class MetalWheel : MonoBehaviour
    {
        public MetalReserves reserves;
        public int hovered = -1;              // 大輪盤用：目前指到哪個扇形（-1 = 沒有）

        float radius, innerRadius;
        bool large;
        readonly RectTransform[] sectorRoots = new RectTransform[MetalInfo.Count];
        readonly RingSectorGraphic[] tracks = new RingSectorGraphic[MetalInfo.Count];
        readonly RingSectorGraphic[] fills = new RingSectorGraphic[MetalInfo.Count];
        readonly RingSectorGraphic[] glows = new RingSectorGraphic[MetalInfo.Count];
        readonly RingSectorGraphic[] outlines = new RingSectorGraphic[MetalInfo.Count];
        readonly Text[] labels = new Text[MetalInfo.Count];
        Text centerText;

        const float Gap = 1.5f;               // 扇形之間留的空隙（度）

        public RectTransform Rect => (RectTransform)transform;
        public float Radius => radius;

        public static MetalWheel Create(Transform parent, MetalReserves reserves, float radius, bool large)
        {
            var go = new GameObject(large ? "MetalWheel_Large" : "MetalWheel_Small", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var wheel = go.AddComponent<MetalWheel>();
            wheel.reserves = reserves;
            wheel.radius = radius;
            wheel.innerRadius = radius * 0.38f;
            wheel.large = large;
            wheel.Build();
            return wheel;
        }

        void Build()
        {
            // ---- 中央的胃：深色底 + 兩圈歪扭的輪廓 ----
            var stomachR = radius * 0.30f;
            var belly = RingSectorGraphic.Create("StomachFill", transform, RingSectorGraphic.Mode.Fill,
                                                 new Color(0.08f, 0.06f, 0.07f, 0.85f), 0f, stomachR, 0f, 360f);
            belly.wobble = 0.07f;
            var wall = RingSectorGraphic.Create("StomachWall", transform, RingSectorGraphic.Mode.Outline,
                                                new Color(0.95f, 0.9f, 0.85f, 0.9f), 0f, stomachR, 0f, 360f);
            wall.wobble = 0.07f;
            wall.SetThickness(large ? 4f : 2f);
            var lining = RingSectorGraphic.Create("StomachLining", transform, RingSectorGraphic.Mode.Outline,
                                                  new Color(0.95f, 0.9f, 0.85f, 0.45f), 0f, stomachR * 0.72f, 30f, 360f);
            lining.wobble = 0.09f;
            lining.SetThickness(large ? 2.5f : 1.5f);

            // ---- 8 個金屬扇形 ----
            for (int i = 0; i < MetalInfo.Count; i++)
            {
                // 每個扇形放在自己的子物件底下，滑鼠指到時整組往外推
                var root = new GameObject(MetalInfo.Names[i], typeof(RectTransform)).GetComponent<RectTransform>();
                root.SetParent(transform, false);
                sectorRoots[i] = root;

                float start = MetalInfo.StartAngle(i) - Gap;
                float sweep = -(MetalInfo.SectorAngle - 2f * Gap);     // 負數 = 順時針
                var col = MetalInfo.Colors[i];

                tracks[i] = RingSectorGraphic.Create("Track", root, RingSectorGraphic.Mode.Fill,
                                                     new Color(0.05f, 0.05f, 0.08f, 0.55f), innerRadius, radius, start, sweep);
                fills[i] = RingSectorGraphic.Create("Reserve", root, RingSectorGraphic.Mode.Fill,
                                                    col, innerRadius, radius, start, sweep);
                glows[i] = RingSectorGraphic.Create("Glow", root, RingSectorGraphic.Mode.Outline,
                                                    col, innerRadius, radius, start, sweep);
                glows[i].soft = true;
                glows[i].SetThickness(large ? 26f : 14f);
                outlines[i] = RingSectorGraphic.Create("Outline", root, RingSectorGraphic.Mode.Outline,
                                                       new Color(1, 1, 1, 0.35f), innerRadius, radius, start, sweep);

                if (large)
                {
                    var label = NewText(root, MetalInfo.Names[i], 26);
                    label.rectTransform.anchoredPosition = MetalInfo.Direction(MetalInfo.MidAngle(i)) * (radius + 48f);
                    labels[i] = label;
                }
            }

            if (large)
            {
                centerText = NewText(transform, "", 22);
                centerText.rectTransform.sizeDelta = new Vector2(radius * 0.6f, 90f);
            }
        }

        Text NewText(Transform parent, string s, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = s;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.rectTransform.sizeDelta = new Vector2(160, 40);
            go.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            return t;
        }

        void Update()
        {
            if (reserves == null) return;
            float t = Time.unscaledTime;

            for (int i = 0; i < MetalInfo.Count; i++)
            {
                float amount = reserves.amount[i];
                bool burning = reserves.burning[i];
                bool hover = i == hovered;
                var col = MetalInfo.Colors[i];

                // 存量：外半徑 = 內半徑 + (總寬度 × 存量)
                fills[i].SetOuterRadius(Mathf.Lerp(innerRadius, radius, amount));

                // 燃燒中：粗外框（金屬色偏亮）+ 光暈呼吸閃爍；每種金屬的閃爍相位錯開
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f + i);
                outlines[i].SetThickness(burning ? (large ? 6f : 3.5f) : (large ? 2f : 1.2f));
                outlines[i].color = burning ? Color.Lerp(col, Color.white, 0.35f) : new Color(1, 1, 1, hover ? 0.9f : 0.3f);
                var g = col;
                g.a = burning ? Mathf.Lerp(0.35f, 0.85f, pulse) : 0f;
                glows[i].color = g;

                // 用完的金屬：底色偏紅提示
                tracks[i].color = amount <= 0f ? new Color(0.35f, 0.05f, 0.05f, 0.6f)
                                                : new Color(0.05f, 0.05f, 0.08f, hover ? 0.8f : 0.55f);

                // 大輪盤：指到的扇形往外凸出一點，文字放大
                var want = hover ? MetalInfo.Direction(MetalInfo.MidAngle(i)) * radius * 0.07f : Vector2.zero;
                sectorRoots[i].anchoredPosition = Vector2.Lerp(sectorRoots[i].anchoredPosition, want,
                                                               1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
                if (labels[i] != null)
                {
                    labels[i].fontSize = hover ? 32 : 26;
                    labels[i].color = burning ? Color.Lerp(col, Color.white, 0.5f) : Color.white;
                }
            }

            if (centerText != null)
            {
                if (hovered < 0) centerText.text = "Drag to a metal";
                else
                {
                    string action = reserves.amount[hovered] <= 0f ? "EMPTY"
                                  : reserves.burning[hovered] ? "Extinguish" : "Burn";
                    centerText.text = $"{MetalInfo.Names[hovered]}\n{action}  {reserves.amount[hovered] * 100f:0}%";
                }
            }
        }

        /// 扇形中線、靠外側的位置（給 UI 粒子用，Wheel 的區域座標）。
        public Vector2 SectorPoint(int i) => MetalInfo.Direction(MetalInfo.MidAngle(i)) * Mathf.Lerp(innerRadius, radius, 0.7f);
    }
}
