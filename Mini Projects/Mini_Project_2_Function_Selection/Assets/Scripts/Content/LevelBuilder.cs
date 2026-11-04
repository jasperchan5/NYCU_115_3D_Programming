using System.Collections.Generic;
using UnityEngine;

namespace FunctionSelection
{
    /// 建好的關卡資訊：出生點、示範路線，以及劇情需要的位置。
    public class LevelInfo
    {
        public Transform root;
        public Vector3 playerSpawn;
        public List<Vector3> tourPoints = new List<Vector3>();   // 示範模式的自動導覽路線
        public float radius;                                    // 可活動範圍（現在由圍籬、山壁、城牆擋住，不另外限制）
        public int objectCount;                                 // 迴圈產生的物件數（報告用）

        // 劇情（StoryDirector）用的位置
        public Transform storyProps;                            // 酒瓶和兩袋金屬粉（喝完會藏起來，重玩時再出現）
        public Vector3 checkpointNear, checkpointFar;           // 掉進裂谷時回到的地方（過裂谷前 / 後）
        public Vector3 poleLanding, beaconLanding;              // 拉橋柱 / 拉烽火柱時落地的位置
    }

    /// 
    /// 程序化生成關卡（不使用任何外部模型），所有物件都由迴圈依規則擺放，固定亂數種子：
    ///
    ///   南邊：村莊（起點）── 中央廣場與噴泉、6 間房屋、12 盞路燈、森林、岩石、花叢、圍籬、遠山
    ///   北邊：沿著土路往前（+z）是劇本的第一幕 Starting Area：
    ///         大裂谷與斷橋 → 酒瓶與兩袋金屬粉 → 對岸的鐵橋柱 → 帶鐵刺的木柵欄 → 巨大的城牆 → 遠方的城堡
    ///         兩側是高聳的山壁，只能沿著這條路走。
    ///
    /// 金屬物件只在這裡「蓋出外形」並取好名字（Lamp、BridgePole、WallNail、WallBeacon），
    /// 遊戲開始時由鎔金術那邊（LevelMetals）依名字掛上 MetalObject。
    /// 
    public static class LevelBuilder
    {
        const float VillageRadius = 24f;   // 房屋所在的圓
        const float FenceRadius = 64f;

        // 北邊的劇情區（z 座標，單位公尺）
        public const float CorridorHalf = 14f;  // 山壁之間的半寬
        public const float RiftNearZ = 72f;     // 裂谷的南岸
        public const float RiftFarZ = 82f;      // 裂谷的北岸（寬 10 公尺）
        public const float BarrierZ = 100f;     // 木柵欄
        public const float WallZ = 118f;        // 城牆的南面
        public const float WallHeight = 18f;
        public const float WallThick = 4f;

        public static LevelInfo Build(Transform parent, int seed = 2026)
        {
            var info = new LevelInfo { radius = 1000f };
            var root = new GameObject("Level").transform;
            root.SetParent(parent, false);
            info.root = root;

            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            SetupAtmosphere();

            // ---- 地面：裂谷把地面切成南、北兩塊（用很厚的方塊，裂谷的崖壁就是方塊的側面）----
            var grass = MaterialLib.Get(new Color(0.36f, 0.58f, 0.28f), 0.05f);
            Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, -15f, (RiftNearZ - 160f) / 2f), new Vector3(320f, 30f, RiftNearZ + 160f), grass, collider: true, name: "Ground");
            Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, -15f, (RiftFarZ + 260f) / 2f), new Vector3(320f, 30f, 260f - RiftFarZ), grass, collider: true, name: "GroundNorth");
            Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, -34f, (RiftNearZ + RiftFarZ) / 2f), new Vector3(320f, 2f, RiftFarZ - RiftNearZ),
                     MaterialLib.Get(new Color(0.05f, 0.04f, 0.05f)), name: "RiftBottom");
            Geo.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0), new Vector3(18f, 0.04f, 18f),
                     MaterialLib.Get(new Color(0.72f, 0.66f, 0.56f), 0.1f), name: "Plaza");
            BuildFountain(root);

            var occupied = new List<(Vector3 p, float r)> { (Vector3.zero, 10f) };   // 廣場

            // ---- 房屋：6 間平均分布在圓上，正面朝向廣場；北方留給土路 ----
            int houses = 6;
            var houseAngles = new List<float>();
            for (int i = 0; i < houses; i++)
            {
                float ang = (i + 0.5f) * 360f / houses + 90f;            // 避開正北（土路方向）
                var pos = Polar(VillageRadius, ang);
                var c = Color.HSVToRGB(0.05f + 0.1f * i / houses, 0.35f, 0.92f);
                BuildHouse(root, pos, c, rng);
                occupied.Add((pos, 6.5f));

                // 廣場到房屋的石板路
                var mid = Polar((9f + VillageRadius - 3f) / 2f, ang);
                var path = Geo.Prim(PrimitiveType.Cube, root, mid + Vector3.up * 0.03f,
                                    new Vector3(2.2f, 0.05f, VillageRadius - 12f), MaterialLib.Get(new Color(0.62f, 0.56f, 0.48f)), name: "Path");
                path.transform.rotation = Quaternion.LookRotation(pos.normalized);
                houseAngles.Add(ang);
                info.objectCount++;
            }

            // ---- 路燈：12 盞在廣場外圈（鐵製，燒鐵時藍線會指過來），每隔一盞真的打光（效能考量）----
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f + 15f;
                var p = Polar(12f, ang);
                var lamp = Geo.Pivot("Lamp", root, p);
                Geo.Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, 1.5f, 0), new Vector3(0.15f, 1.5f, 0.15f),
                         MaterialLib.Get(new Color(0.15f, 0.15f, 0.18f), 0.6f, 0.6f), collider: true);
                Geo.Prim(PrimitiveType.Sphere, lamp, new Vector3(0, 3.15f, 0), Vector3.one * 0.45f, MaterialLib.Emissive(new Color(1f, 0.82f, 0.45f), 3f));
                if (i % 2 == 0) Geo.PointLight(lamp, new Vector3(0, 3.1f, 0), new Color(1f, 0.8f, 0.5f), 9f, 1.6f);
                info.objectCount++;
            }

            // ---- 北邊的劇情區 ----
            info.objectCount += BuildNorth(root, info, rng);

            // 示範模式的導覽路線：從廣場沿石板路走到每間房屋門口，再走到北邊土路的盡頭，最後回廣場
            var stops = new List<(float ang, float r)>();
            foreach (var a in houseAngles) stops.Add((a, VillageRadius - 6.5f));
            stops.Add((90f, RiftNearZ - 10f));
            stops.Sort((u, v) => Mathf.Repeat(u.ang, 360f).CompareTo(Mathf.Repeat(v.ang, 360f)));
            foreach (var (a, r) in stops)
            {
                info.tourPoints.Add(Polar(8f, a));
                info.tourPoints.Add(Polar(r, a));
                info.tourPoints.Add(Polar(8f, a));
            }

            // ---- 森林：拒絕取樣，避開房屋、北邊的走廊與彼此 ----
            int trees = 0, tries = 0;
            var treePos = new List<Vector3>();
            while (trees < 90 && tries++ < 5000)
            {
                var p = Polar(R(32f, FenceRadius - 4f), R(0f, 360f));
                if (Blocked(p, occupied, 0f) || InCorridor(p, 3f)) continue;
                bool crowded = false;
                foreach (var q in treePos) if ((q - p).sqrMagnitude < 3.6f * 3.6f) { crowded = true; break; }
                if (crowded) continue;
                treePos.Add(p);
                BuildTree(root, p, rng);
                trees++;
            }
            foreach (var p in treePos) occupied.Add((p, 1.2f));
            info.objectCount += trees;

            // ---- 岩石 40 顆 ----
            int rocks = 0; tries = 0;
            while (rocks < 40 && tries++ < 3000)
            {
                var p = Polar(R(11f, FenceRadius - 3f), R(0f, 360f));
                if (Blocked(p, occupied, 1f) || InCorridor(p, 2f)) continue;
                float s = R(0.6f, 2.0f);
                var g = R(0.42f, 0.58f);
                Geo.Prim(PrimitiveType.Sphere, root, p + Vector3.up * s * 0.25f, new Vector3(s * R(1f, 1.6f), s * 0.7f, s),
                         MaterialLib.Get(new Color(g, g, g * 1.05f), 0.15f), new Vector3(0, R(0, 360), 0), collider: true, name: "Rock");
                occupied.Add((p, s));
                rocks++;
            }
            info.objectCount += rocks;

            // ---- 花叢 36 叢：沿著房屋圓圈內側 ----
            Color[] flowerColors = { new Color(0.95f, 0.35f, 0.45f), new Color(0.98f, 0.85f, 0.3f), new Color(0.6f, 0.45f, 0.95f), Color.white };
            int flowers = 0;
            for (int i = 0; i < 36; i++)
            {
                float fa = i * 10f + R(-3f, 3f);
                bool onPath = Mathf.Abs(Mathf.DeltaAngle(fa, 90f)) < 8f;          // 北邊的土路
                foreach (var a in houseAngles) onPath |= Mathf.Abs(Mathf.DeltaAngle(fa, a)) < 8f;
                if (onPath) continue;                                            // 路上不放花
                flowers++;
                var p = Polar(R(14.5f, 17f), fa);
                var bush = Geo.Pivot("FlowerBush", root, p);
                Geo.Prim(PrimitiveType.Sphere, bush, new Vector3(0, 0.3f, 0), new Vector3(1.1f, 0.6f, 1.1f), MaterialLib.Get(new Color(0.22f, 0.5f, 0.2f)));
                var fc = flowerColors[i % flowerColors.Length];
                for (int k = 0; k < 4; k++)
                    Geo.Prim(PrimitiveType.Sphere, bush, new Vector3(R(-0.4f, 0.4f), 0.6f, R(-0.4f, 0.4f)), Vector3.one * 0.18f, MaterialLib.Get(fc, 0.4f));
            }
            info.objectCount += flowers;

            // ---- 圍籬：每 6 度一根柱子，柱子間用橫木相連；北邊留一個缺口接到走廊 ----
            int posts = 60, built = 0;
            for (int i = 0; i < posts; i++)
            {
                float a0 = i * 360f / posts, a1 = (i + 1) * 360f / posts;
                var p0 = Polar(FenceRadius, a0);
                var p1 = Polar(FenceRadius, a1);
                if (p0.z > 0f && Mathf.Abs(p0.x) < CorridorHalf + 1f) continue;   // 缺口：走廊的入口
                var wood = MaterialLib.Get(new Color(0.45f, 0.32f, 0.2f));
                Geo.Prim(PrimitiveType.Cube, root, p0 + Vector3.up * 0.7f, new Vector3(0.3f, 1.4f, 0.3f), wood, collider: true, name: "FencePost");
                built++;
                if (p1.z > 0f && Mathf.Abs(p1.x) < CorridorHalf + 1f) continue;   // 缺口旁邊那根柱子不接橫木
                var mid = (p0 + p1) / 2f;
                var len = (p1 - p0).magnitude;
                var rot = Quaternion.LookRotation(p1 - p0).eulerAngles;
                Geo.Prim(PrimitiveType.Cube, root, mid + Vector3.up * 1.0f, new Vector3(0.12f, 0.15f, len), wood, rot, collider: true, name: "FenceRail");
                Geo.Prim(PrimitiveType.Cube, root, mid + Vector3.up * 0.5f, new Vector3(0.12f, 0.15f, len), wood, rot, collider: true, name: "FenceRail");
            }
            info.objectCount += built;

            // ---- 遠山：圍籬外一圈，讓地平線有層次（北邊留給城牆和城堡）----
            int mountains = 0;
            for (int i = 0; i < 18; i++)
            {
                float a = i * 20f + R(-6f, 6f);
                float h = R(18f, 38f);
                var g = new Color(R(0.32f, 0.4f), R(0.45f, 0.55f), R(0.42f, 0.5f));
                if (Mathf.Abs(Mathf.DeltaAngle(a, 90f)) < 40f) continue;
                Geo.Prim(PrimitiveType.Sphere, root, Polar(R(95f, 120f), a), new Vector3(R(35f, 55f), h * 2f, R(35f, 55f)), MaterialLib.Get(g, 0.05f), name: "Mountain");
                mountains++;
            }
            info.objectCount += mountains;

            info.playerSpawn = new Vector3(0, 0.2f, -8f);
            BuildFireflies(root);
            return info;
        }

        /// 北邊的走廊範圍（|x| < 半寬 + pad、在村莊北半邊）：樹和石頭不放這裡。
        static bool InCorridor(Vector3 p, float pad) => p.z > 8f && Mathf.Abs(p.x) < CorridorHalf + pad;

        static bool Blocked(Vector3 p, List<(Vector3 p, float r)> occupied, float pad)
        {
            foreach (var (q, r) in occupied)
                if ((q - p).sqrMagnitude < (r + pad) * (r + pad)) return true;
            return false;
        }

        /// 極座標轉位置，0 度 = +x，90 度 = +z（北）。
        public static Vector3 Polar(float r, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        static void SetupAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 320f;          // 拉遠一點，城牆和遠方的城堡才看得到
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.92f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.55f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.2f);

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                l.color = new Color(1f, 0.93f, 0.82f);
                l.intensity = 1.3f;
                l.shadows = LightShadows.Soft;
            }
        }

        // =====================================================================
        // 北邊：劇本第一幕 Starting Area
        // =====================================================================

        /// 回傳用迴圈產生的物件數。
        static int BuildNorth(Transform root, LevelInfo info, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int count = 0;
            var dirt = MaterialLib.Get(new Color(0.48f, 0.36f, 0.24f), 0.05f);
            var cliffRock = MaterialLib.Get(new Color(0.4f, 0.38f, 0.37f), 0.1f);
            var wood = MaterialLib.Get(new Color(0.42f, 0.29f, 0.17f));
            var iron = MaterialLib.Get(new Color(0.22f, 0.22f, 0.25f), 0.6f, 0.9f);
            var stone = MaterialLib.Get(new Color(0.6f, 0.58f, 0.55f), 0.15f);

            // ---- 土路：廣場 → 裂谷，裂谷對岸 → 城牆 ----
            Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.03f, (9f + RiftNearZ) / 2f), new Vector3(3f, 0.05f, RiftNearZ - 9f), dirt, name: "DirtPath");
            Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.03f, (RiftFarZ + WallZ) / 2f), new Vector3(3f, 0.05f, WallZ - RiftFarZ), dirt, name: "DirtPath");

            // ---- 兩側山壁：每 6 公尺一塊，高度隨機；往下一直延伸到裂谷底，往外接到圍籬 ----
            for (float z = 52f; z < WallZ + 2f; z += 6f)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float h = R(13f, 19f);
                    float x = side * (CorridorHalf + 4f + R(-0.6f, 0.6f));
                    Geo.Prim(PrimitiveType.Cube, root, new Vector3(x, (h - 36f) / 2f, z), new Vector3(8f, h + 36f, 6.6f), cliffRock,
                             new Vector3(0, R(-4f, 4f), 0), collider: true, name: "Cliff");
                    count++;
                }
            }

            // ---- 斷橋：南岸一段、北岸一段，中間斷掉 ----
            // 木板每 0.5 公尺一片（迴圈），靠近斷口的幾片歪斜、往下垂
            for (float z = RiftNearZ - 6f; z < RiftNearZ + 2.6f; z += 0.5f)
            {
                float broken = Mathf.InverseLerp(RiftNearZ, RiftNearZ + 2.6f, z);    // 0 = 完整，1 = 斷口
                Geo.Prim(PrimitiveType.Cube, root, new Vector3(R(-0.1f, 0.1f), 0.12f - broken * 0.6f, z), new Vector3(3f - broken * R(0.5f, 1.6f), 0.1f, 0.42f),
                         wood, new Vector3(broken * 25f, R(-3f, 3f), R(-4f, 4f) * broken), collider: true, name: "Plank");
                count++;
            }
            for (float z = RiftFarZ - 2.6f; z < RiftFarZ + 5f; z += 0.5f)
            {
                float broken = Mathf.InverseLerp(RiftFarZ, RiftFarZ - 2.6f, z);
                Geo.Prim(PrimitiveType.Cube, root, new Vector3(R(-0.1f, 0.1f), 0.12f - broken * 0.6f, z), new Vector3(3f - broken * R(0.5f, 1.6f), 0.1f, 0.42f),
                         wood, new Vector3(-broken * 25f, R(-3f, 3f), R(-4f, 4f) * broken), collider: true, name: "Plank");
                count++;
            }
            // 南岸的木樁（木頭，不是金屬）與斷掉垂下的繩子
            foreach (float x in new[] { -1.8f, 1.8f })
            {
                Geo.Prim(PrimitiveType.Cylinder, root, new Vector3(x, 1f, RiftNearZ - 5.5f), new Vector3(0.3f, 1f, 0.3f), wood, collider: true, name: "BridgePost");
                Geo.Prim(PrimitiveType.Cylinder, root, new Vector3(x, -1.5f, RiftNearZ + 0.4f), new Vector3(0.05f, 1.8f, 0.05f), wood, new Vector3(-15f, 0, 0), name: "Rope");
                Geo.Prim(PrimitiveType.Cylinder, root, new Vector3(x, -1.5f, RiftFarZ - 0.4f), new Vector3(0.05f, 1.8f, 0.05f), wood, new Vector3(15f, 0, 0), name: "Rope");
            }
            // 北岸的兩根鐵橋柱：拉它們會飛過裂谷，落在北岸的橋面上（落腳點 Landing）
            info.poleLanding = new Vector3(0f, 0.2f, RiftFarZ + 3.5f);
            foreach (float x in new[] { -1.8f, 1.8f })
            {
                var pole = Geo.Pivot("BridgePole", root, new Vector3(x, 0f, RiftFarZ + 1.5f));
                Geo.Prim(PrimitiveType.Cylinder, pole, new Vector3(0, 2f, 0), new Vector3(0.3f, 2f, 0.3f), iron, collider: true);
                Geo.Prim(PrimitiveType.Sphere, pole, new Vector3(0, 4.1f, 0), Vector3.one * 0.45f, iron);
                Geo.Prim(PrimitiveType.Cube, pole, new Vector3(0, 3.2f, 0), new Vector3(0.5f, 0.12f, 0.5f), iron);
                var landing = Geo.Pivot("Landing", pole, Vector3.zero);
                landing.position = info.poleLanding;
            }
            info.checkpointNear = new Vector3(0f, 0.2f, RiftNearZ - 8f);
            info.checkpointFar = info.poleLanding;

            // ---- 酒瓶與兩袋金屬粉（劇情道具）----
            var props = Geo.Pivot("StoryProps", root, new Vector3(-3.4f, 0f, RiftNearZ - 4.5f));
            info.storyProps = props;
            var glass = MaterialLib.Get(new Color(0.25f, 0.55f, 0.3f), 0.95f);
            Geo.Prim(PrimitiveType.Cylinder, props, new Vector3(0, 0.12f, 0), new Vector3(0.22f, 0.3f, 0.22f), glass, new Vector3(90f, 30f, 0), name: "Bottle");
            Geo.Prim(PrimitiveType.Cylinder, props, new Vector3(0.12f, 0.12f, 0.33f), new Vector3(0.1f, 0.1f, 0.1f), glass, new Vector3(90f, 30f, 0), name: "BottleNeck");
            Geo.Prim(PrimitiveType.Cylinder, props, new Vector3(0, 0.01f, 0.1f), new Vector3(0.6f, 0.005f, 0.5f), MaterialLib.Get(new Color(0.55f, 0.45f, 0.3f), 0.9f), name: "Spill");
            var sack = MaterialLib.Get(new Color(0.55f, 0.42f, 0.28f));
            var powder = MaterialLib.Get(new Color(0.7f, 0.72f, 0.76f), 0.85f, 0.8f);   // 灰色、閃亮
            for (int i = 0; i < 2; i++)
            {
                var p = new Vector3(0.7f + i * 0.6f, 0f, -0.4f + i * 0.5f);
                Geo.Prim(PrimitiveType.Sphere, props, p + Vector3.up * 0.2f, new Vector3(0.45f, 0.4f, 0.4f), sack, new Vector3(0, i * 50f, 15f), name: "Bag");
                Geo.Prim(PrimitiveType.Sphere, props, p + new Vector3(0.25f, 0.03f, 0.1f), new Vector3(0.4f, 0.06f, 0.3f), powder, name: "Powder");
            }

            // ---- 柵欄區：兩側大石頭把路縮成 10 公尺寬，木柵欄的位置留給遊戲開始時放（BarrierSpot）----
            foreach (int side in new[] { -1, 1 })
            {
                Geo.Prim(PrimitiveType.Cube, root, new Vector3(side * 9.8f, 2.5f, BarrierZ), new Vector3(8.6f, 5f, 3f), cliffRock,
                         new Vector3(R(-5f, 5f), R(-8f, 8f), 0), collider: true, name: "Boulder");
            }
            foreach (float x in new[] { -3.4f, 0f, 3.4f })
                Geo.Pivot("BarrierSpot", root, new Vector3(x, 0f, BarrierZ));

            // ---- 金屬藥瓶的位置（遊戲開始時由鎔金術那邊放上藥瓶）----
            Geo.Pivot("VialSpot_Iron", root, new Vector3(4f, 0.8f, RiftNearZ - 6f));
            Geo.Pivot("VialSpot_Iron", root, new Vector3(-4f, 0.8f, RiftFarZ + 7f));
            Geo.Pivot("VialSpot_Steel", root, new Vector3(5f, 0.8f, BarrierZ - 7f));
            Geo.Pivot("VialSpot_Iron", root, new Vector3(-6f, 0.8f, WallZ - 6f));
            Geo.Pivot("VialSpot_Steel", root, new Vector3(6f, 0.8f, WallZ - 6f));

            count += BuildWall(root, info, rng, stone, iron);
            count += BuildCastle(root, rng);
            return count;
        }

        /// 巨大的城牆：牆身、牆面上一排排鐵釘、牆頂的城垛、石磚步道，中間一根鐵製烽火柱。
        static int BuildWall(Transform root, LevelInfo info, System.Random rng, Material stone, Material iron)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int count = 0;
            var wall = Geo.Pivot("Wall", root, Vector3.zero);
            float midZ = WallZ + WallThick / 2f;

            // 牆身：14 段，每段 20 公尺，顏色稍有不同
            for (int i = 0; i < 14; i++)
            {
                float g = R(0.5f, 0.6f);
                Geo.Prim(PrimitiveType.Cube, wall, new Vector3(-130f + i * 20f, WallHeight / 2f, midZ), new Vector3(20.05f, WallHeight, WallThick),
                         MaterialLib.Get(new Color(g, g * 0.97f, g * 0.92f), 0.1f), collider: true, name: "WallSegment");
                count++;
            }

            // 城垛：牆頂前後兩排，每 3 公尺一個（只做中間 80 公尺，遠處看不到）
            for (float x = -40f; x <= 40f; x += 3f)
            {
                foreach (float z in new[] { WallZ + 0.4f, WallZ + WallThick - 0.4f })
                {
                    Geo.Prim(PrimitiveType.Cube, wall, new Vector3(x, WallHeight + 0.6f, z), new Vector3(1.6f, 1.2f, 0.8f), stone, collider: true, name: "Merlon");
                    count++;
                }
            }

            // 牆頂的石磚步道（中間 12 公尺，深淺交錯）
            for (int ix = -5; ix <= 5; ix++)
            {
                for (int iz = 0; iz < 2; iz++)
                {
                    var c = (ix + iz) % 2 == 0 ? new Color(0.62f, 0.6f, 0.56f) : new Color(0.55f, 0.53f, 0.5f);
                    Geo.Prim(PrimitiveType.Cube, wall, new Vector3(ix * 1.2f, WallHeight + 0.03f, WallZ + 1.4f + iz * 1.2f), new Vector3(1.15f, 0.06f, 1.15f),
                             MaterialLib.Get(c, 0.1f), name: "Brick");
                    count++;
                }
            }

            // 牆面上的鐵釘：3 排 × 9 根（燒鐵時藍線會指過來）
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    var nail = Geo.Pivot("WallNail", wall, new Vector3(-24f + col * 6f, 5f + row * 4f, WallZ - 0.05f));
                    Geo.Prim(PrimitiveType.Cylinder, nail, Vector3.zero, new Vector3(0.6f, 0.08f, 0.6f), iron, new Vector3(90f, 0, 0));      // 釘頭
                    Geo.Prim(PrimitiveType.Cylinder, nail, new Vector3(0, 0, 0.3f), new Vector3(0.18f, 0.3f, 0.18f), iron, new Vector3(90f, 0, 0));
                    count++;
                }
            }

            // 城牆後面（北邊）的石磚廣場：落地的地方。10 × 11 塊，深淺交錯
            for (int ix = 0; ix < 10; ix++)
            {
                for (int iz = 0; iz < 11; iz++)
                {
                    var c = (ix + iz) % 2 == 0 ? new Color(0.6f, 0.58f, 0.54f) : new Color(0.5f, 0.48f, 0.45f);
                    Geo.Prim(PrimitiveType.Cube, wall, new Vector3(-11.4f + ix * 2.4f, 0.03f, WallZ + WallThick + 1.2f + iz * 2.4f),
                             new Vector3(2.35f, 0.06f, 2.35f), MaterialLib.Get(c, 0.1f), name: "CourtyardBrick");
                    count++;
                }
            }

            // 牆頂中央的鐵製烽火柱：拉它就能飛上牆頂，落在旁邊的石磚上（落腳點 Landing）
            // 落腳點在兩個城垛的缺口後面（城垛在 x = -1、2，缺口是 -0.2 ~ 1.2）
            info.beaconLanding = new Vector3(0.5f, WallHeight + 0.2f, WallZ + 1.3f);
            var beacon = Geo.Pivot("WallBeacon", wall, new Vector3(0f, WallHeight, WallZ + 2.4f));
            Geo.Prim(PrimitiveType.Cylinder, beacon, new Vector3(0, 1.8f, 0), new Vector3(0.35f, 1.8f, 0.35f), iron, collider: true);
            Geo.Prim(PrimitiveType.Cylinder, beacon, new Vector3(0, 3.7f, 0), new Vector3(1.1f, 0.2f, 1.1f), iron, name: "Brazier");
            Geo.Prim(PrimitiveType.Sphere, beacon, new Vector3(0, 4.1f, 0), new Vector3(0.8f, 0.6f, 0.8f), MaterialLib.Emissive(new Color(1f, 0.55f, 0.15f), 4f), name: "Fire");
            Geo.PointLight(beacon, new Vector3(0, 4.4f, 0), new Color(1f, 0.6f, 0.25f), 14f, 2.5f);
            var landing = Geo.Pivot("Landing", beacon, Vector3.zero);
            landing.position = info.beaconLanding;
            return count;
        }

        /// 牆後遠方的城堡：主堡 + 6 座塔（每座塔頂用越來越細的圓柱疊出尖頂）+ 發光的窗戶。
        static int BuildCastle(Transform root, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var castle = Geo.Pivot("Castle", root, new Vector3(0f, 0f, WallZ + 75f));
            var body = MaterialLib.Get(new Color(0.45f, 0.45f, 0.5f), 0.1f);
            var roof = MaterialLib.Get(new Color(0.3f, 0.2f, 0.35f), 0.2f);
            var glow = MaterialLib.Emissive(new Color(1f, 0.8f, 0.45f), 2.5f);

            Geo.Prim(PrimitiveType.Cube, castle, new Vector3(0, 15f, 0), new Vector3(40f, 30f, 26f), body, name: "Keep");
            for (int i = 0; i < 6; i++)
            {
                float x = -30f + i * 12f;
                float h = (i == 2 || i == 3) ? R(50f, 58f) : R(36f, 46f);     // 中間兩座最高
                float z = (i % 2 == 0) ? -10f : 6f;
                var tower = Geo.Pivot("Tower", castle, new Vector3(x, 0f, z));
                Geo.Prim(PrimitiveType.Cylinder, tower, new Vector3(0, h / 2f, 0), new Vector3(7f, h / 2f, 7f), body);
                for (int k = 0; k < 5; k++)
                {
                    float w = 8f - k * 1.5f;
                    Geo.Prim(PrimitiveType.Cylinder, tower, new Vector3(0, h + 1f + k * 2.2f, 0), new Vector3(w, 1.1f, w), roof);
                }
                for (int k = 0; k < 3; k++)
                    Geo.Prim(PrimitiveType.Cube, tower, new Vector3(0, h * (0.45f + k * 0.15f), -3.55f), new Vector3(0.9f, 1.6f, 0.1f), glow, name: "Window");
            }
            return 7;
        }

        static void BuildFountain(Transform root)
        {
            var stone = MaterialLib.Get(new Color(0.78f, 0.76f, 0.72f), 0.3f);
            var water = MaterialLib.Get(new Color(0.25f, 0.55f, 0.85f), 0.95f);
            var f = Geo.Pivot("Fountain", root, Vector3.zero);
            Geo.Prim(PrimitiveType.Cylinder, f, new Vector3(0, 0.35f, 0), new Vector3(5f, 0.35f, 5f), stone, collider: true);
            Geo.Prim(PrimitiveType.Cylinder, f, new Vector3(0, 0.66f, 0), new Vector3(4.4f, 0.02f, 4.4f), water, name: "Water");
            Geo.Prim(PrimitiveType.Cylinder, f, new Vector3(0, 1.4f, 0), new Vector3(0.6f, 1.0f, 0.6f), stone, collider: true);
            Geo.Prim(PrimitiveType.Cylinder, f, new Vector3(0, 2.4f, 0), new Vector3(2f, 0.15f, 2f), stone);
            Geo.Prim(PrimitiveType.Cylinder, f, new Vector3(0, 2.56f, 0), new Vector3(1.7f, 0.02f, 1.7f), water);

            // 噴泉水花（粒子）
            var go = new GameObject("Spray");
            go.transform.SetParent(f, false);
            go.transform.localPosition = new Vector3(0, 2.6f, 0);
            go.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.3f;
            main.startSpeed = 5.5f;
            main.startSize = 0.14f;
            main.startColor = new Color(0.75f, 0.9f, 1f, 0.9f);
            main.gravityModifier = 1.1f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 120f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 14f;
            sh.radius = 0.1f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MaterialLib.Particle(new Color(0.8f, 0.92f, 1f));
        }

        static void BuildHouse(Transform root, Vector3 pos, Color wallColor, System.Random rng)
        {
            var h = Geo.Pivot("House", root, pos);
            h.rotation = Quaternion.LookRotation(-pos.normalized);   // 正面（+z）朝向廣場

            var wall = MaterialLib.Get(wallColor, 0.1f);
            var roof = MaterialLib.Get(new Color(0.62f, 0.22f, 0.18f), 0.2f);
            var wood = MaterialLib.Get(new Color(0.38f, 0.25f, 0.15f));
            var glow = MaterialLib.Emissive(new Color(1f, 0.8f, 0.45f), 2.5f);

            Geo.Prim(PrimitiveType.Cube, h, new Vector3(0, 2f, 0), new Vector3(6f, 4f, 5f), wall, collider: true, name: "Walls");
            // 斜屋頂：兩片傾斜的板子組成人字形
            Geo.Prim(PrimitiveType.Cube, h, new Vector3(-1.6f, 4.9f, 0), new Vector3(3.9f, 0.25f, 5.8f), roof, new Vector3(0, 0, 35f));
            Geo.Prim(PrimitiveType.Cube, h, new Vector3(1.6f, 4.9f, 0), new Vector3(3.9f, 0.25f, 5.8f), roof, new Vector3(0, 0, -35f));
            Geo.Prim(PrimitiveType.Cube, h, new Vector3(0, 4.6f, 0), new Vector3(4.6f, 1.6f, 4.9f), wall, name: "Gable");
            Geo.Prim(PrimitiveType.Cube, h, new Vector3(1.6f, 5.6f, -1.2f), new Vector3(0.7f, 1.6f, 0.7f), MaterialLib.Get(new Color(0.5f, 0.45f, 0.42f)), name: "Chimney");
            // 門與窗
            Geo.Prim(PrimitiveType.Cube, h, new Vector3(0, 1.1f, 2.51f), new Vector3(1.2f, 2.2f, 0.08f), wood, name: "Door");
            Geo.Prim(PrimitiveType.Sphere, h, new Vector3(0.4f, 1.1f, 2.58f), Vector3.one * 0.12f, MaterialLib.Get(new Color(0.9f, 0.75f, 0.3f), 0.8f, 0.8f));
            foreach (float x in new[] { -1.9f, 1.9f })
            {
                Geo.Prim(PrimitiveType.Cube, h, new Vector3(x, 2.3f, 2.51f), new Vector3(1.0f, 0.9f, 0.06f), glow, name: "Window");
                Geo.Prim(PrimitiveType.Cube, h, new Vector3(x, 1.75f, 2.6f), new Vector3(1.2f, 0.12f, 0.2f), wood, name: "Sill");
            }
            // 門口的木桶
            if (rng.NextDouble() < 0.7)
                Geo.Prim(PrimitiveType.Cylinder, h, new Vector3(-2.3f, 0.45f, 3.2f), new Vector3(0.7f, 0.45f, 0.7f), wood, collider: true, name: "Barrel");
        }

        static void BuildTree(Transform root, Vector3 p, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var t = Geo.Pivot("Tree", root, p);
            t.rotation = Quaternion.Euler(0, R(0, 360), 0);
            float s = R(0.8f, 1.4f);
            t.localScale = Vector3.one * s;
            var bark = MaterialLib.Get(new Color(0.4f, 0.27f, 0.16f));
            Geo.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1.2f, 0), new Vector3(0.45f, 1.2f, 0.45f), bark, collider: true, name: "Trunk");

            var leaf = MaterialLib.Get(Color.HSVToRGB(R(0.24f, 0.36f), R(0.5f, 0.75f), R(0.35f, 0.6f)), 0.1f);
            if (rng.NextDouble() < 0.5)
            {
                // 松樹：越往上越窄的層層枝葉
                for (int i = 0; i < 4; i++)
                {
                    float w = 3.2f - i * 0.7f;
                    Geo.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 2.4f + i * 0.9f, 0), new Vector3(w, 0.45f, w), leaf);
                }
            }
            else
            {
                // 闊葉樹：幾團圓形樹冠
                Geo.Prim(PrimitiveType.Sphere, t, new Vector3(0, 3.4f, 0), Vector3.one * 3.0f, leaf);
                for (int i = 0; i < 3; i++)
                {
                    var off = Quaternion.Euler(0, i * 120f, 0) * new Vector3(0.9f, 0, 0);
                    Geo.Prim(PrimitiveType.Sphere, t, new Vector3(off.x, 3.0f + R(-0.2f, 0.4f), off.z), Vector3.one * R(1.6f, 2.2f), leaf);
                }
            }
        }

        /// 廣場上空飄動的螢火蟲（粒子）。
        static void BuildFireflies(Transform root)
        {
            var go = new GameObject("Fireflies");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, 2.5f, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 6f;
            main.startSpeed = 0.3f;
            main.startSize = 0.12f;
            main.startColor = new Color(1f, 0.95f, 0.5f);
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 25f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 22f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.3f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MaterialLib.Particle(new Color(1f, 0.92f, 0.45f));
        }
    }
}