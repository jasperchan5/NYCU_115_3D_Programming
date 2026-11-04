using System.Collections.Generic;
using UnityEngine;

namespace PuzzleMaster
{
    ///
    /// 初始平台：遺忘之地入口的圓形石台。全部用程式產生（primitive），物件用迴圈擺放、固定亂數種子：
    ///   地面、圓形石台（外圈 24 塊石磚、中央發光的魔法陣）、南邊 3 階樓梯、
    ///   8 根石柱（有幾根斷掉）、4 座火盆（真的會發光）
    /// 然後把三個角色（銀時、神樂、伊莉莎白）的模型生成在石台上。
    ///
    /// 模型放在 Assets/Resources/Model/ 底下，用 Resources.Load 依路徑載入，不需要在場景裡手動擺放。
    /// 三個模型的原始大小、原點位置都不一樣，所以載入後用「包圍盒」統一處理：
    ///   縮放：讓模型的高度 = 指定的身高
    ///   位置：讓模型的腳底（包圍盒最低點）剛好踩在石台上、水平中心對準站位
    ///
    public static class StartingArea
    {
        public const float PlatformRadius = 9f;
        public const float PlatformTop = 0.6f;          // 石台表面的高度

        /// 角色的資料：Resources 裡的路徑、身高（公尺）、站的位置、額外旋轉（修正模型本身的朝向）。
        struct CharacterSpec
        {
            public string name, path;
            public float height;
            public Vector3 position;
            public float yaw;
            public CharacterSpec(string name, string path, float height, Vector3 position, float yaw = 0f)
            { this.name = name; this.path = path; this.height = height; this.position = position; this.yaw = yaw; }
        }

        static readonly CharacterSpec[] Characters =
        {
            new CharacterSpec("Gintoki", "Model/Gintoki/source/gintoki", 1.8f, new Vector3(0f, PlatformTop, 0f)),
            new CharacterSpec("Kagura", "Model/Kagura/source/kagura", 1.55f, new Vector3(-2.2f, PlatformTop, 0.6f)),
            new CharacterSpec("Elizabeth", "Model/Elizabeth/source/Erizabesu", 1.9f, new Vector3(2.4f, PlatformTop, 0.6f)),
        };

        public static Dictionary<string, Transform> Build(Transform parent)
        {
            var root = new GameObject("StartingArea").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(2026);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var grass = Mat(new Color(0.3f, 0.45f, 0.25f), 0.05f);
            var stone = Mat(new Color(0.62f, 0.6f, 0.56f), 0.15f);
            var darkStone = Mat(new Color(0.45f, 0.43f, 0.41f), 0.1f);
            var iron = Mat(new Color(0.2f, 0.2f, 0.22f), 0.6f, 0.8f);

            // ---- 地面 ----
            Prim(PrimitiveType.Cube, root, new Vector3(0f, -0.5f, 0f), new Vector3(200f, 1f, 200f), grass, collider: true, name: "Ground");

            // ---- 圓形石台 ----
            Prim(PrimitiveType.Cylinder, root, new Vector3(0f, PlatformTop / 2f, 0f), new Vector3(PlatformRadius * 2f, PlatformTop / 2f, PlatformRadius * 2f),
                 stone, collider: true, name: "Platform");

            // 外圈石磚：24 塊，深淺交錯，每塊轉向圓心
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                var tile = Prim(PrimitiveType.Cube, root, Polar(7.6f, a) + Vector3.up * (PlatformTop + 0.01f), new Vector3(2.2f, 0.04f, 1.6f),
                                i % 2 == 0 ? darkStone : stone, name: "Tile");
                tile.transform.rotation = Quaternion.LookRotation(-Polar(1f, a));
            }

            // 中央的魔法陣：兩圈發光的細環 + 五個小光點（五個水晶封印）
            var rune = Glow(new Color(0.35f, 0.8f, 1f), 1.5f);
            Prim(PrimitiveType.Cylinder, root, new Vector3(0f, PlatformTop + 0.005f, 0f), new Vector3(6.4f, 0.005f, 6.4f), rune, name: "RuneOuter");
            Prim(PrimitiveType.Cylinder, root, new Vector3(0f, PlatformTop + 0.01f, 0f), new Vector3(6.0f, 0.005f, 6.0f), stone, name: "RuneGap");
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Sphere, root, Polar(2.6f, 90f + i * 72f) + Vector3.up * (PlatformTop + 0.05f), new Vector3(0.35f, 0.08f, 0.35f), rune, name: "SealMark");

            // ---- 南邊的樓梯：3 階，越下面越寬 ----
            for (int i = 0; i < 3; i++)
            {
                float h = PlatformTop * (3 - i) / 3f;
                Prim(PrimitiveType.Cube, root, new Vector3(0f, h / 2f, -PlatformRadius - 0.4f - i * 0.6f), new Vector3(4f + i * 0.8f, h, 0.8f + i * 0.6f),
                     stone, collider: true, name: "Step");
            }

            // ---- 石柱：8 根在外緣，有幾根斷掉（高度隨機），南邊留給樓梯 ----
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 22.5f;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 30f) continue;           // 樓梯那一側不放
                bool broken = rng.NextDouble() < 0.35;
                float h = broken ? R(1.2f, 2.2f) : R(4f, 4.6f);
                var p = Polar(PlatformRadius - 0.7f, a) + Vector3.up * PlatformTop;
                Prim(PrimitiveType.Cylinder, root, p + Vector3.up * h / 2f, new Vector3(0.8f, h / 2f, 0.8f), stone, collider: true, name: "Pillar");
                Prim(PrimitiveType.Cube, root, p + Vector3.up * 0.15f, new Vector3(1.1f, 0.3f, 1.1f), darkStone, name: "PillarBase");
                if (broken)   // 斷掉的上半截倒在旁邊
                    Prim(PrimitiveType.Cylinder, root, p + Polar(1.4f, a + 90f) + Vector3.up * 0.4f, new Vector3(0.75f, R(0.8f, 1.2f), 0.75f),
                         stone, new Vector3(90f, a, 0f), collider: true, name: "FallenPillar");
                else
                    Prim(PrimitiveType.Cube, root, p + Vector3.up * (h + 0.15f), new Vector3(1.0f, 0.3f, 1.0f), darkStone, name: "PillarCap");
            }

            // ---- 火盆：4 座，在石柱之間 ----
            for (int i = 0; i < 4; i++)
            {
                var p = Polar(PlatformRadius - 1.6f, i * 90f + 45f) + Vector3.up * PlatformTop;
                var brazier = new GameObject("Brazier").transform;
                brazier.SetParent(root, false);
                brazier.position = p;
                Prim(PrimitiveType.Cylinder, brazier, new Vector3(0f, 0.5f, 0f), new Vector3(0.15f, 0.5f, 0.15f), iron, collider: true);
                Prim(PrimitiveType.Cylinder, brazier, new Vector3(0f, 1.05f, 0f), new Vector3(0.7f, 0.1f, 0.7f), iron, name: "Bowl");
                Prim(PrimitiveType.Sphere, brazier, new Vector3(0f, 1.25f, 0f), new Vector3(0.45f, 0.4f, 0.45f), Glow(new Color(1f, 0.55f, 0.15f), 4f), name: "Fire");
                var light = new GameObject("FireLight").AddComponent<Light>();
                light.transform.SetParent(brazier, false);
                light.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.6f, 0.25f);
                light.range = 7f;
                light.intensity = 2f;
            }

            SetupLighting();
            SetupCamera();

            // ---- 角色 ----
            var spawned = new Dictionary<string, Transform>();
            foreach (var c in Characters)
            {
                var t = SpawnModel(root, c);
                if (t != null) spawned[c.name] = t;
            }
            return spawned;
        }

        ///
        /// 載入模型並放到站位上：
        ///   1. Resources.Load 依路徑取得模型，Instantiate 產生一份
        ///   2. 外面包一個空物件（之後移動、轉向都動這個外層），模型放在裡面
        ///   3. 用所有 Renderer 的包圍盒算出模型目前的高度 → 縮放到指定身高
        ///   4. 再算一次包圍盒，把模型往回移：水平中心對準外層的原點、腳底剛好在外層的原點高度
        ///
        static Transform SpawnModel(Transform parent, CharacterSpec c)
        {
            var prefab = Resources.Load<GameObject>(c.path);
            if (prefab == null)
            {
                Debug.LogWarning($"[StartingArea] 找不到模型 Resources/{c.path}（模型要放在 Assets/Resources/ 底下）");
                return null;
            }

            var holder = new GameObject(c.name).transform;
            holder.SetParent(parent, false);
            holder.position = c.position;
            holder.rotation = Quaternion.Euler(0f, 180f, 0f);          // 面向南邊（鏡頭的方向）

            var model = Object.Instantiate(prefab, holder);
            model.name = "Model";
            model.transform.localRotation = Quaternion.Euler(0f, c.yaw, 0f);

            var b = WorldBounds(model);
            if (b.size.y > 0.001f) model.transform.localScale *= c.height / b.size.y;

            b = WorldBounds(model);
            var offset = new Vector3(holder.position.x - b.center.x, holder.position.y - b.min.y, holder.position.z - b.center.z);
            model.transform.position += offset;
            return holder;
        }

        /// 模型所有零件（Renderer）的包圍盒合起來。
        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static void SetupLighting()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 140f;
            RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.55f);

            var sun = Object.FindAnyObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
        }

        /// 暫時的鏡頭：從樓梯前方看向三個角色（之後做玩家移動時再換成跟隨鏡頭）。
        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(0f, 3.4f, -10f);
            cam.transform.LookAt(new Vector3(0f, 1.6f, 0.3f));
            cam.farClipPlane = 300f;
        }

        // ------------------------------------------------------------------
        // 小工具
        // ------------------------------------------------------------------

        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Material Mat(Color c, float smoothness = 0.2f, float metallic = 0f)
        {
            string key = $"L{c}{smoothness}{metallic}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return cache[key] = m;
        }

        /// 自己發光的材質（魔法陣、火焰）：URP Lit 打開 Emission。
        static Material Glow(Color c, float intensity)
        {
            string key = $"G{c}{intensity}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            return cache[key] = m;
        }

        static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat,
                               Vector3 euler = default, bool collider = false, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// 極座標 → 位置（0° = +x，90° = +z 北方）。
        static Vector3 Polar(float r, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
    }
}