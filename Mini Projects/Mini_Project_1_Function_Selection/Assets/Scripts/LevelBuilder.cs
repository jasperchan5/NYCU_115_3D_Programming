using System.Collections.Generic;
using UnityEngine;

namespace FunctionSelection
{
    /// 建好的關卡資訊：出生點、寶石與怪物的位置。
    public class LevelInfo
    {
        public Transform root;
        public Vector3 playerSpawn;
        public List<Vector3> gemPoints = new List<Vector3>();
        public List<Vector3> monsterHomes = new List<Vector3>();
        public List<Vector3> tourPoints = new List<Vector3>();   // 示範模式的自動導覽路線
        public float radius;                                    // 可活動範圍
        public int objectCount;                                 // 迴圈產生的物件數（報告用）
    }

    /// 
    /// 程序化生成「神秘村莊」關卡：中央廣場與噴泉、6 間房屋、路燈、森林、岩石、花叢、
    /// 圍籬、山洞與遠山。所有物件都由迴圈依規則擺放（不使用任何外部模型）。
    /// 固定亂數種子，所以每次產生的場景都一樣。
    /// 
    public static class LevelBuilder
    {
        const float VillageRadius = 24f;   // 房屋所在的圓
        const float FenceRadius = 64f;
        static readonly Vector3 CavePos = new Vector3(0f, 0f, 46f);

        public static LevelInfo Build(Transform parent, int seed = 2026)
        {
            var info = new LevelInfo { radius = FenceRadius - 3f };
            var root = new GameObject("Level").transform;
            root.SetParent(parent, false);
            info.root = root;

            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            SetupAtmosphere();

            // ---- 地面與廣場 ----
            Geo.Prim(PrimitiveType.Plane, root, Vector3.zero, new Vector3(30f, 1f, 30f),
                     MaterialLib.Get(new Color(0.36f, 0.58f, 0.28f), 0.05f), collider: true, name: "Ground");
            Geo.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0), new Vector3(18f, 0.04f, 18f),
                     MaterialLib.Get(new Color(0.72f, 0.66f, 0.56f), 0.1f), name: "Plaza");
            BuildFountain(root);

            var occupied = new List<(Vector3 p, float r)>
            {
                (Vector3.zero, 10f),                       // 廣場
                (CavePos, 9f),                             // 洞口
                (CavePos + new Vector3(0, 0, 6f), 12f),    // 山體
            };
            // 廣場通往山洞的步道：不能種樹、放石頭
            for (float z = 10f; z < CavePos.z - 6f; z += 3f) occupied.Add((new Vector3(0, 0, z), 2.5f));

            // ---- 房屋：6 間平均分布在圓上，正面朝向廣場；北方留給山洞 ----
            int houses = 6;
            var houseAngles = new List<float>();
            for (int i = 0; i < houses; i++)
            {
                float ang = (i + 0.5f) * 360f / houses + 90f;            // 避開正北（山洞方向）
                var pos = Polar(VillageRadius, ang);
                var c = Color.HSVToRGB(0.05f + 0.1f * i / houses, 0.35f, 0.92f);
                BuildHouse(root, pos, c, rng);
                occupied.Add((pos, 6.5f));

                // 廣場到房屋的石板路
                var mid = Polar((9f + VillageRadius - 3f) / 2f, ang);
                var path = Geo.Prim(PrimitiveType.Cube, root, mid + Vector3.up * 0.03f,
                                    new Vector3(2.2f, 0.05f, VillageRadius - 12f), MaterialLib.Get(new Color(0.62f, 0.56f, 0.48f)), name: "Path");
                path.transform.rotation = Quaternion.LookRotation(pos.normalized);

                // 每間房屋前一顆寶石、屋後一個怪物出沒點
                info.gemPoints.Add(Polar(VillageRadius - 6f, ang) + Vector3.up * 1f);
                info.monsterHomes.Add(Polar(VillageRadius + 10f, ang + 20f));
                houseAngles.Add(ang);
                info.objectCount++;
            }

            // ---- 路燈：12 盞在廣場外圈，每隔一盞真的打光（效能考量）----
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

            // ---- 山洞（北方）----
            BuildCave(root, rng);
            info.gemPoints.Add(CavePos + new Vector3(0, 1f, -4.5f));      // 洞口前（山洞朝南）
            Geo.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.03f, (9f + CavePos.z - 5f) / 2f),
                     new Vector3(2.2f, 0.05f, CavePos.z - 14f), MaterialLib.Get(new Color(0.62f, 0.56f, 0.48f)), name: "CavePath");

            // 示範模式的導覽路線：從廣場沿石板路走到每間房屋門口（和山洞）再走回廣場
            var stops = new List<(float ang, float r)>();
            foreach (var a in houseAngles) stops.Add((a, VillageRadius - 6.5f));
            stops.Add((90f, CavePos.z - 7f));
            stops.Sort((u, v) => Mathf.Repeat(u.ang, 360f).CompareTo(Mathf.Repeat(v.ang, 360f)));
            foreach (var (a, r) in stops)
            {
                info.tourPoints.Add(Polar(8f, a));
                info.tourPoints.Add(Polar(r, a));
                info.tourPoints.Add(Polar(8f, a));
            }

            // ---- 森林：拒絕取樣，避開房屋、山洞與彼此 ----
            int trees = 0, tries = 0;
            var treePos = new List<Vector3>();
            while (trees < 90 && tries++ < 5000)
            {
                var p = Polar(R(32f, FenceRadius - 4f), R(0f, 360f));
                if (Blocked(p, occupied, 0f)) continue;
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
                if (Blocked(p, occupied, 1f)) continue;
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
                bool onPath = Mathf.Abs(Mathf.DeltaAngle(fa, 90f)) < 8f;          // 山洞步道
                foreach (var a in houseAngles) onPath |= Mathf.Abs(Mathf.DeltaAngle(fa, a)) < 8f;
                if (onPath) continue;                                            // 石板路上不放花
                flowers++;
                var p = Polar(R(14.5f, 17f), fa);
                var bush = Geo.Pivot("FlowerBush", root, p);
                Geo.Prim(PrimitiveType.Sphere, bush, new Vector3(0, 0.3f, 0), new Vector3(1.1f, 0.6f, 1.1f), MaterialLib.Get(new Color(0.22f, 0.5f, 0.2f)));
                var fc = flowerColors[i % flowerColors.Length];
                for (int k = 0; k < 4; k++)
                    Geo.Prim(PrimitiveType.Sphere, bush, new Vector3(R(-0.4f, 0.4f), 0.6f, R(-0.4f, 0.4f)), Vector3.one * 0.18f, MaterialLib.Get(fc, 0.4f));
            }
            info.objectCount += flowers;

            // ---- 圍籬：每 6 度一根柱子，柱子間用橫木相連 ----
            int posts = 60;
            for (int i = 0; i < posts; i++)
            {
                float a0 = i * 360f / posts, a1 = (i + 1) * 360f / posts;
                var p0 = Polar(FenceRadius, a0);
                var p1 = Polar(FenceRadius, a1);
                var wood = MaterialLib.Get(new Color(0.45f, 0.32f, 0.2f));
                Geo.Prim(PrimitiveType.Cube, root, p0 + Vector3.up * 0.7f, new Vector3(0.3f, 1.4f, 0.3f), wood, collider: true, name: "FencePost");
                var mid = (p0 + p1) / 2f;
                var len = (p1 - p0).magnitude;
                var rot = Quaternion.LookRotation(p1 - p0).eulerAngles;
                Geo.Prim(PrimitiveType.Cube, root, mid + Vector3.up * 1.0f, new Vector3(0.12f, 0.15f, len), wood, rot, collider: true, name: "FenceRail");
                Geo.Prim(PrimitiveType.Cube, root, mid + Vector3.up * 0.5f, new Vector3(0.12f, 0.15f, len), wood, rot, collider: true, name: "FenceRail");
            }
            info.objectCount += posts;

            // ---- 遠山：圍籬外一圈，讓地平線有層次 ----
            for (int i = 0; i < 18; i++)
            {
                var p = Polar(R(95f, 120f), i * 20f + R(-6f, 6f));
                float h = R(18f, 38f);
                var g = new Color(R(0.32f, 0.4f), R(0.45f, 0.55f), R(0.42f, 0.5f));
                Geo.Prim(PrimitiveType.Sphere, root, p, new Vector3(R(35f, 55f), h * 2f, R(35f, 55f)), MaterialLib.Get(g, 0.05f), name: "Mountain");
            }
            info.objectCount += 18;

            // ---- 其他寶石：散在森林邊緣（總共 20 顆）----
            int extra = 20 - info.gemPoints.Count;
            for (int i = 0; i < extra; i++)
            {
                var p = Polar(R(30f, 50f), i * (360f / extra) + R(-8f, 8f));
                if (Blocked(p, occupied, 0.5f)) p = Polar(29f, i * (360f / extra));
                info.gemPoints.Add(p + Vector3.up * 1f);
            }

            info.playerSpawn = new Vector3(0, 0.2f, -6f);
            BuildFireflies(root);
            return info;
        }

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
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 210f;
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

        static void BuildCave(Transform root, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var cave = Geo.Pivot("Cave", root, CavePos);
            cave.rotation = Quaternion.LookRotation(-CavePos.normalized);   // 洞口朝南（朝廣場）
            var rock = MaterialLib.Get(new Color(0.42f, 0.4f, 0.44f), 0.1f);
            var dark = MaterialLib.Get(new Color(0.03f, 0.03f, 0.05f), 0f);

            // 山體
            Geo.Prim(PrimitiveType.Sphere, cave, new Vector3(0, 2f, -6f), new Vector3(20f, 14f, 14f), rock, collider: true, name: "Hill");
            // 洞口內部的黑暗
            Geo.Prim(PrimitiveType.Sphere, cave, new Vector3(0, 2.2f, 0.4f), new Vector3(6.5f, 6f, 3f), dark, name: "Mouth");
            // 拱門：沿半圓擺 13 顆大石頭
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.PI * i / 12f;
                var p = new Vector3(Mathf.Cos(a) * 4.2f, Mathf.Sin(a) * 4.6f + 0.6f, 1.6f);
                float s = R(1.6f, 2.3f);
                Geo.Prim(PrimitiveType.Sphere, cave, p, new Vector3(s, s, s * 1.3f), rock, new Vector3(R(0, 360), R(0, 360), 0), collider: true, name: "ArchRock");
            }
            // 洞裡發光的水晶
            for (int i = 0; i < 7; i++)
            {
                var p = new Vector3(R(-2.4f, 2.4f), R(0.3f, 0.9f), R(2.0f, 2.8f));   // 洞口前、拱門腳之間
                Geo.Prim(PrimitiveType.Cube, cave, p, new Vector3(0.25f, R(0.6f, 1.4f), 0.25f),
                         MaterialLib.Emissive(new Color(0.35f, 0.8f, 1f), 3f), new Vector3(R(-20, 20), R(0, 90), R(-20, 20)), name: "Crystal");
            }
            Geo.PointLight(cave, new Vector3(0, 1.4f, 3.2f), new Color(0.4f, 0.8f, 1f), 10f, 2.5f);

            // 洞口兩側的火把
            foreach (float x in new[] { -5.2f, 5.2f })
            {
                var torch = Geo.Pivot("Torch", cave, new Vector3(x, 0, 3.2f));
                Geo.Prim(PrimitiveType.Cylinder, torch, new Vector3(0, 1f, 0), new Vector3(0.15f, 1f, 0.15f), MaterialLib.Get(new Color(0.35f, 0.22f, 0.12f)), collider: true);
                Geo.Prim(PrimitiveType.Sphere, torch, new Vector3(0, 2.15f, 0), Vector3.one * 0.35f, MaterialLib.Emissive(new Color(1f, 0.5f, 0.15f), 4f));
                Geo.PointLight(torch, new Vector3(0, 2.3f, 0.3f), new Color(1f, 0.55f, 0.2f), 8f, 2f);
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
