using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Allomancy
{
    /// 
    /// 燃燒青銅時：每個敵人頭上出現一個小金屬環，正在燒的金屬那一格亮起來（位置跟大輪盤一樣）。
    /// 這是 Screen Space Overlay 的 UI，畫在所有 3D 物體上面 → 隔著障礙物也看得到。
    /// 敵人燒紅銅時（Hidden）整個環都是暗的：看不出他在燒什麼。
    ///
    /// 另外：被驚動的搜尋者頭上會顯示紅色「!」（不需要燒青銅），讓玩家知道自己被發現了。
    /// 
    public class BronzeSight : MonoBehaviour
    {
        MetalReserves reserves;
        Camera view;
        RectTransform canvasRect;
        readonly Dictionary<Enemy, Badge> badges = new Dictionary<Enemy, Badge>();

        const float Radius = 26f, Inner = 9f, MaxRange = 45f;

        /// 一個敵人頭上的小環。
        class Badge
        {
            public RectTransform root, wheel;
            public RingSectorGraphic[] fills = new RingSectorGraphic[MetalInfo.Count];
            public RingSectorGraphic[] glows = new RingSectorGraphic[MetalInfo.Count];
            public Text alert;
        }

        public static BronzeSight Create(Canvas canvas, MetalReserves reserves, Camera view)
        {
            var s = new GameObject("BronzeSight", typeof(RectTransform)).AddComponent<BronzeSight>();
            s.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)s.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            s.reserves = reserves;
            s.view = view;
            s.canvasRect = (RectTransform)canvas.transform;
            return s;
        }

        Badge MakeBadge()
        {
            var b = new Badge();
            b.root = new GameObject("EnemyBadge", typeof(RectTransform)).GetComponent<RectTransform>();
            b.root.SetParent(transform, false);
            b.wheel = new GameObject("Wheel", typeof(RectTransform)).GetComponent<RectTransform>();
            b.wheel.SetParent(b.root, false);

            // 底：深色圓盤
            RingSectorGraphic.Create("Back", b.wheel, RingSectorGraphic.Mode.Fill, new Color(0.04f, 0.04f, 0.06f, 0.6f), 0f, Radius + 3f, 0f, 360f);
            for (int i = 0; i < MetalInfo.Count; i++)
            {
                float start = MetalInfo.StartAngle(i) - 2f;
                float sweep = -(MetalInfo.SectorAngle - 4f);           // 負數 = 順時針，留 2° 間隙
                var col = MetalInfo.Colors[i];
                // 暗的底格（金屬色、很淡）→ 看得出每格是哪個金屬
                RingSectorGraphic.Create("Track", b.wheel, RingSectorGraphic.Mode.Fill, new Color(col.r, col.g, col.b, 0.18f), Inner, Radius, start, sweep);
                // 亮格：正在燒才顯示
                b.fills[i] = RingSectorGraphic.Create("Lit", b.wheel, RingSectorGraphic.Mode.Fill, col, Inner, Radius, start, sweep);
                b.glows[i] = RingSectorGraphic.Create("Glow", b.wheel, RingSectorGraphic.Mode.Outline, col, Inner, Radius, start, sweep);
                b.glows[i].soft = true;
                b.glows[i].SetThickness(8f);
            }
            RingSectorGraphic.Create("Rim", b.wheel, RingSectorGraphic.Mode.Outline, new Color(0.8f, 0.6f, 0.35f, 0.8f), 0f, Radius + 3f, 0f, 360f).SetThickness(1.5f);

            // 搜尋者警戒的「!」
            b.alert = new GameObject("Alert", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<Text>();
            b.alert.transform.SetParent(b.root, false);
            b.alert.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            b.alert.fontSize = 34;
            b.alert.fontStyle = FontStyle.Bold;
            b.alert.alignment = TextAnchor.MiddleCenter;
            b.alert.raycastTarget = false;
            b.alert.text = "!";
            b.alert.color = new Color(1f, 0.25f, 0.15f);
            b.alert.rectTransform.sizeDelta = new Vector2(40f, 40f);
            b.alert.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            return b;
        }

        void LateUpdate()
        {
            bool bronze = reserves.IsBurning(Metal.Bronze);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);

            foreach (var e in Enemy.All)
            {
                if (!badges.TryGetValue(e, out var b)) badges[e] = b = MakeBadge();
                var ea = e.GetComponent<EnemyAllomancy>();
                bool alerted = ea != null && ea.Alerted;

                // 頭頂上方一點的位置 → 螢幕座標（z < 0 = 在鏡頭後面，不畫）
                var head = e.transform.position + Vector3.up * 2.9f;
                var sp = view.WorldToScreenPoint(head);
                float dist = Vector3.Distance(view.transform.position, head);
                bool show = sp.z > 0f && dist < MaxRange && (bronze || alerted);
                b.root.gameObject.SetActive(show);
                if (!show) continue;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
                b.root.anchoredPosition = local;
                b.root.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.55f, dist / MaxRange);   // 越遠越小

                b.wheel.gameObject.SetActive(bronze);
                if (bronze)
                {
                    for (int i = 0; i < MetalInfo.Count; i++)
                    {
                        // 敵人燒紅銅 → Hidden，全部不亮
                        bool lit = ea != null && !ea.Hidden && ea.burning[i];
                        b.fills[i].enabled = lit;
                        b.glows[i].enabled = lit;
                        if (lit)
                        {
                            var g = MetalInfo.Colors[i];
                            g.a = 0.6f * pulse;
                            b.glows[i].color = g;
                        }
                    }
                }

                // 「!」放在小環上方；沒燒青銅時放在原位
                b.alert.gameObject.SetActive(alerted);
                b.alert.rectTransform.anchoredPosition = new Vector2(0f, bronze ? Radius + 22f : 0f);
                b.alert.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 12f));
            }
        }
    }
}
