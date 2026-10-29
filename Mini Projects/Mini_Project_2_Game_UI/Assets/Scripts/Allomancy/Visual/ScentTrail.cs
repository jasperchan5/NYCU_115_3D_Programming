using UnityEngine;

namespace Allomancy
{
    /// 
    /// 燃燒錫時「聞得到」地上的金屬瓶：瓶子上方冒出三條彎彎曲曲往上飄的氣味線。
    ///
    /// 每條線是一個 LineRenderer，沿著高度取 Points 個點：
    ///     高度 h = f × Height                    （f = 0~1，從瓶口到頂端）
    ///     左右擺動 = sin(f × 9 − 時間 × 2.5 + 相位) × 振幅
    ///     振幅隨高度變大（越往上散得越開）
    /// 「− 時間」讓波形往上移動，看起來就像氣味一直往上飄。
    /// 顏色用漸層：瓶口附近透明 → 中段最濃 → 頂端淡出。
    /// 瓶子被撿走（還沒重生）時不顯示。
    /// 
    public class ScentTrail : MonoBehaviour
    {
        MetalReserves reserves;
        MetalVial vial;
        Transform player;
        readonly LineRenderer[] strands = new LineRenderer[3];
        readonly Gradient gradient = new Gradient();
        float fade;

        const int Points = 24;
        const float Height = 3.2f, SmellRange = 30f;
        static readonly Color ScentColor = new Color(0.75f, 0.9f, 1f);   // 跟瓶子一樣的淡藍色

        public static ScentTrail Attach(MetalVial vial, MetalReserves reserves, Transform player)
        {
            var s = new GameObject("ScentTrail").AddComponent<ScentTrail>();
            s.transform.SetParent(vial.transform.parent, false);     // 不放在瓶子底下：瓶子會旋轉、上下浮動
            s.vial = vial;
            s.reserves = reserves;
            s.player = player;
            var mat = Build.GlowTransparent(Color.white);           // 顏色由頂點色（漸層）決定
            for (int i = 0; i < s.strands.Length; i++)
            {
                var lr = new GameObject("Strand").AddComponent<LineRenderer>();
                lr.transform.SetParent(s.transform, false);
                lr.positionCount = Points;
                lr.useWorldSpace = true;
                lr.sharedMaterial = mat;
                lr.startWidth = 0.04f;
                lr.endWidth = 0.14f;                                 // 往上越來越寬、越散
                lr.numCapVertices = 4;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                s.strands[i] = lr;
            }
            return s;
        }

        void Update()
        {
            var origin = vial.transform.position + Vector3.up * 0.35f;      // 瓶口
            bool want = reserves.IsBurning(Metal.Tin) && vial.Available
                        && Vector3.Distance(player.position, origin) < SmellRange;
            fade = Mathf.MoveTowards(fade, want ? 1f : 0f, Time.deltaTime * 2f);
            bool visible = fade > 0.001f;
            foreach (var s in strands) s.enabled = visible;
            if (!visible) return;

            // 漸層：0% 透明 → 25% 最濃 → 100% 透明（再乘上 fade 做淡入淡出）
            float a = 0.55f * fade;
            gradient.SetKeys(
                new[] { new GradientColorKey(ScentColor, 0f), new GradientColorKey(ScentColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(a, 0.25f), new GradientAlphaKey(0f, 1f) });

            float t = Time.time;
            for (int s = 0; s < strands.Length; s++)
            {
                var lr = strands[s];
                lr.colorGradient = gradient;
                float phase = s * 2.1f;
                for (int p = 0; p < Points; p++)
                {
                    float f = p / (Points - 1f);
                    float amp = 0.06f + 0.35f * f;
                    float x = Mathf.Sin(f * 9f - t * 2.5f + phase) * amp;
                    float z = Mathf.Cos(f * 7f - t * 2.0f + phase * 1.7f) * amp * 0.6f;
                    lr.SetPosition(p, origin + new Vector3(x, f * Height, z));
                }
            }
        }
    }
}
