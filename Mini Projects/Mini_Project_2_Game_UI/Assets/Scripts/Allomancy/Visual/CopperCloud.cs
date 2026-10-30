using System.Collections.Generic;
using UnityEngine;

namespace Allomancy
{
    /// 
    /// 燃燒紅銅時：玩家胸口（從背後看就是背上正中央）出現一小團旋轉的半透明紅銅雲。
    /// 照草圖：一團拳頭大小、捲來捲去的雲，不會擋住畫面。
    ///
    /// 做法：幾顆小的半透明球繞著同一個中心轉，每顆的路徑由兩個不同頻率的圓疊加
    ///     位置 = (cos a, sin a) × R1 + (cos 2.3a, sin 1.7a) × R2
    /// 兩個頻率不成整數比 → 路徑互相交錯，看起來像一團捲動的雲（跟草圖的螺旋線一樣）。
    /// 中間再放一顆較大、較淡的球當作雲的「核心」。
    /// 整團雲繞著玩家公轉：每幀把雲的中心放在胸口高度、半徑 OrbitRadius 的水平圓上
    ///     位置 = (sin θ, 上下浮動, cos θ) × 半徑，θ = 時間 × OrbitSpeed
    /// 雲掛在 Chest 底下，所以玩家移動、轉身時軌道會跟著。
    /// 實際的遮蔽效果在 EnemyAllomancy.PlayerPulsing()：燒紅銅時搜尋者感覺不到你。
    /// 
    public class CopperCloud : MonoBehaviour
    {
        MetalReserves reserves;
        Transform root;
        Material puffMat, coreMat;
        readonly List<Transform> puffs = new List<Transform>();
        Transform core;
        float fade;

        const int PuffCount = 6;
        const float R1 = 0.12f, R2 = 0.06f;           // 整團大約 40 公分寬
        const float PuffAlpha = 0.1f, CoreAlpha = 0.05f;
        const float OrbitRadius = 0.6f;              // 公轉半徑（公尺）
        const float OrbitSpeed = 1f;               // 公轉角速度（弧度 / 秒），約 4 秒繞一圈

        public static CopperCloud Create(PlayerController player, MetalReserves reserves)
        {
            var c = player.gameObject.AddComponent<CopperCloud>();
            c.reserves = reserves;
            c.root = new GameObject("CopperCloud").transform;
            c.root.SetParent(player.Chest, false);

            var copper = MetalInfo.Colors[(int)Metal.Copper];
            c.puffMat = new Material(Build.GlowTransparent(copper));      // 自己的材質，不動到快取共用的
            c.coreMat = new Material(Build.GlowTransparent(copper));

            c.core = Build.Prim(PrimitiveType.Sphere, c.root, Vector3.zero, Vector3.one * 0.3f, c.coreMat, name: "CloudCore").transform;
            NoShadow(c.core.gameObject);
            for (int i = 0; i < PuffCount; i++)
            {
                var p = Build.Prim(PrimitiveType.Sphere, c.root, Vector3.zero, Vector3.one * 0.12f, c.puffMat, name: "Puff");
                NoShadow(p);
                c.puffs.Add(p.transform);
            }
            c.root.gameObject.SetActive(false);
            return c;
        }

        static void NoShadow(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void Update()
        {
            bool on = reserves.IsBurning(Metal.Copper);
            fade = Mathf.MoveTowards(fade, on ? 1f : 0f, Time.deltaTime * 3f);
            bool visible = fade > 0.001f;
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return;

            var col = MetalInfo.Colors[(int)Metal.Copper];
            puffMat.color = new Color(col.r, col.g, col.b, PuffAlpha * fade);
            coreMat.color = new Color(col.r, col.g, col.b, CoreAlpha * fade);

            float t = Time.time;

            // 公轉：在胸口高度的水平圓上繞圈，加一點上下浮動；雲本身轉向面對玩家（中心）
            float theta = t * OrbitSpeed;
            root.localPosition = new Vector3(Mathf.Sin(theta) * OrbitRadius,
                                             Mathf.Sin(theta * 2f) * 0.08f,
                                             Mathf.Cos(theta) * OrbitRadius);
            root.localRotation = Quaternion.Euler(0f, theta * Mathf.Rad2Deg, 0f);
            for (int i = 0; i < puffs.Count; i++)
            {
                // 每顆的起始角度平均分開（2π / 6），一起繞圈；兩個頻率疊加 → 捲來捲去
                float a = t * 2.2f + i * (2f * Mathf.PI / PuffCount);
                var pos = new Vector3(Mathf.Cos(a) * R1 + Mathf.Cos(a * 2.3f) * R2,
                                      Mathf.Sin(a) * R1 + Mathf.Sin(a * 1.7f) * R2,
                                      Mathf.Sin(a * 1.3f) * 0.03f);
                puffs[i].localPosition = pos * fade;                                 // 淡入時從中心捲出來
                puffs[i].localScale = Vector3.one * (0.1f + 0.04f * Mathf.Sin(t * 3f + i)) * fade;
            }
            core.localScale = Vector3.one * 0.3f * fade * (1f + 0.08f * Mathf.Sin(t * 2f));
        }
    }
}