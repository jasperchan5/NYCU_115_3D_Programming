using System.Collections.Generic;
using UnityEngine;

namespace Allomancy
{
    /// 
    /// 建立場景物件的小工具：primitive、URP 材質（依顏色快取）、點光源。
    /// 
    public static class Build
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader lit, unlit;

        public static Material Mat(Color c, float smoothness = 0.3f, float metallic = 0f)
        {
            string key = $"{c}{smoothness}{metallic}";
            if (cache.TryGetValue(key, out var m)) return m;
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(lit);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            cache[key] = m;
            return m;
        }

        /// 自己發光、不受光影響的材質（情緒核心、燈火、鋼線）。
        public static Material Glow(Color c)
        {
            string key = "glow" + c;
            if (cache.TryGetValue(key, out var m)) return m;
            if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit");
            m = new Material(unlit);
            m.SetColor("_BaseColor", c);
            cache[key] = m;
            return m;
        }

        public static Material GlowTransparent(Color c)
        {
            string key = "glowT" + c;
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Shader.Find("Sprites/Default"));
            m.color = c;
            cache[key] = m;
            return m;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale,
                                      Material mat, Vector3 euler = default, bool collider = false, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static Light PointLight(Transform parent, Vector3 localPos, Color c, float range, float intensity)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = localPos;
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            return l;
        }
    }

    /// 
    /// 含金屬的物體。燃燒鐵 / 鋼時，藍線會指到它的重心。
    /// 所有金屬物件登記在 All 清單裡，SteelSight 與小地圖直接讀這個清單，不需要每幀搜尋場景。
    /// 推 / 拉時誰會動，取決於重量：比玩家重 → 玩家被推開 / 拉過去；比玩家輕 → 物體飛走 / 飛過來。
    /// 
    public class MetalObject : MonoBehaviour
    {
        public static readonly List<MetalObject> All = new List<MetalObject>();
        public const float PlayerMass = 70f;       // 玩家的重量（公斤），用來決定藍線深淺與推拉誰動

        public string displayName = "Metal";
        public float mass = 1f;                    // 公斤；越重藍線越粗
        public Rigidbody body;                     // 推拉時要移動的剛體（沒有 = 固定在地上，不會動）
        public Enemy owner;                        // 敵人身上的金屬：推拉會把整個敵人推開 / 拉近
        Renderer[] renderers;

        // 重量判斷
        // 固定在地上的物體視為無限重
        public bool Anchored => body == null && owner == null;
        public float MovingMass => owner != null ? owner.TotalMass
                         : body != null ? body.mass
                         : mass;          

        public bool HeavierThanPlayer => Anchored || MovingMass > PlayerMass;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            body = GetComponentInParent<Rigidbody>();
            owner = GetComponentInParent<Enemy>();
        }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public Renderer[] Renderers => renderers;

        /// 所有零件的包圍盒合起來（判斷物體是不是放在地上時要用到高度）
        public Bounds WorldBounds
        {
            get
            {
                if (renderers == null || renderers.Length == 0) return new Bounds(transform.position, Vector3.zero);
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return b;
            }
        }

        public Vector3 CenterOfMass => WorldBounds.center;
    }
}
