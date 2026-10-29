using System.Collections.Generic;
using UnityEngine;

namespace Allomancy
{
    /// 
    /// 場景：圓形的石造廣場。所有物件都用迴圈擺放：
    ///   外圍石牆 40 塊、石柱 8 根（上面有鐵火盆）、鐵路燈 12 支、
    ///   木箱 12 個（非金屬）、鐵箍木桶 10 個、錢幣堆 16 堆、中央的鐵雕像。
    /// 金屬物件都掛上 MetalObject，燒鐵 / 鋼時才會有藍線指過去。
    /// 重量（公斤）：雕像 2000、路燈 150、火盆 60（固定不動，推拉時是玩家被推開 / 拉過去）；
    ///              鐵箍 4、錢幣 0.3（有剛體，比玩家輕，會被推飛 / 拉過來）。
    /// 
    public static class Arena
    {
        public const float Radius = 34f;

        public static List<Vector3> Build(Transform parent, out List<Vector3> enemyHomes)
        {
            var root = new GameObject("Arena").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 Polar(float r, float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * r, 0f, Mathf.Sin(deg * Mathf.Deg2Rad) * r);

            var stone = Allomancy.Build.Mat(new Color(0.5f, 0.48f, 0.45f), 0.15f);
            var darkStone = Allomancy.Build.Mat(new Color(0.32f, 0.3f, 0.3f), 0.1f);
            var iron = Allomancy.Build.Mat(new Color(0.3f, 0.3f, 0.33f), 0.6f, 0.9f);
            var wood = Allomancy.Build.Mat(new Color(0.45f, 0.3f, 0.18f));
            var gold = Allomancy.Build.Mat(new Color(0.85f, 0.7f, 0.3f), 0.8f, 1f);

            // 地面與中央的圓形石板
            Allomancy.Build.Prim(PrimitiveType.Plane, root, Vector3.zero, new Vector3(10, 1, 10), Allomancy.Build.Mat(new Color(0.28f, 0.3f, 0.26f)), collider: true, name: "Ground");
            Allomancy.Build.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.02f, 0), new Vector3(Radius * 2f, 0.03f, Radius * 2f), stone, name: "Floor");

            // 外圍石牆：40 塊，每塊轉向圓心
            for (int i = 0; i < 40; i++)
            {
                float a = i * 9f;
                float h = R(3f, 4.5f);
                var wall = Allomancy.Build.Prim(PrimitiveType.Cube, root, Polar(Radius + 1f, a) + Vector3.up * h / 2f,
                                                new Vector3(5.4f, h, 1.2f), darkStone, collider: true, name: "Wall");
                wall.transform.rotation = Quaternion.LookRotation(-Polar(1f, a));
            }

            // 石柱與鐵火盆：8 根
            for (int i = 0; i < 8; i++)
            {
                var p = Polar(22f, i * 45f + 22.5f);
                var pillar = new GameObject("Pillar").transform;
                pillar.SetParent(root, false);
                pillar.position = p;
                Allomancy.Build.Prim(PrimitiveType.Cylinder, pillar, new Vector3(0, 2f, 0), new Vector3(1.2f, 2f, 1.2f), stone, collider: true);
                var brazier = Allomancy.Build.Prim(PrimitiveType.Cylinder, pillar, new Vector3(0, 4.2f, 0), new Vector3(1.0f, 0.2f, 1.0f), iron, name: "Brazier");
                AddMetal(brazier, "Iron brazier", 60f);
                Allomancy.Build.Prim(PrimitiveType.Sphere, pillar, new Vector3(0, 4.5f, 0), new Vector3(0.6f, 0.5f, 0.6f), Allomancy.Build.Glow(new Color(1f, 0.55f, 0.15f)), name: "Fire");
                Allomancy.Build.PointLight(pillar, new Vector3(0, 4.8f, 0), new Color(1f, 0.6f, 0.25f), 12f, 2f);
            }

            // 鐵路燈：12 支，在中圈
            for (int i = 0; i < 12; i++)
            {
                var p = Polar(14f, i * 30f);
                var lamp = new GameObject("LampPost").transform;
                lamp.SetParent(root, false);
                lamp.position = p;
                Allomancy.Build.Prim(PrimitiveType.Cylinder, lamp, new Vector3(0, 1.6f, 0), new Vector3(0.12f, 1.6f, 0.12f), iron, collider: true);
                Allomancy.Build.Prim(PrimitiveType.Cube, lamp, new Vector3(0, 3.3f, 0), new Vector3(0.35f, 0.4f, 0.35f), Allomancy.Build.Glow(new Color(1f, 0.85f, 0.55f)));
                AddMetal(lamp.gameObject, "Iron lamp post", 150f);
            }

            // 木箱（不是金屬，不會有藍線）：12 個，散在外圈
            for (int i = 0; i < 12; i++)
            {
                var p = Polar(R(26f, 31f), i * 30f + R(-8f, 8f));
                float s = R(0.8f, 1.4f);
                Allomancy.Build.Prim(PrimitiveType.Cube, root, p + Vector3.up * s / 2f, Vector3.one * s, wood,
                                     new Vector3(0, R(0, 90), 0), collider: true, name: "Crate");
            }

            // 鐵箍木桶：10 個，木頭桶身 + 兩圈鐵箍（鐵箍是金屬）
            for (int i = 0; i < 10; i++)
            {
                var p = Polar(R(17f, 20f), i * 36f + 15f);
                var barrel = new GameObject("Barrel").transform;
                barrel.SetParent(root, false);
                barrel.position = p;
                Allomancy.Build.Prim(PrimitiveType.Cylinder, barrel, new Vector3(0, 0.6f, 0), new Vector3(0.8f, 0.6f, 0.8f), wood, collider: true);
                var rb = barrel.gameObject.AddComponent<Rigidbody>();      // 木桶會被推動：先加剛體，MetalObject 才找得到
                rb.mass = 25f + 4f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                var hoops = new GameObject("Hoops").transform;
                hoops.SetParent(barrel, false);
                foreach (float y in new[] { 0.25f, 0.95f })
                    Allomancy.Build.Prim(PrimitiveType.Cylinder, hoops, new Vector3(0, y, 0), new Vector3(0.84f, 0.04f, 0.84f), iron);
                AddMetal(hoops.gameObject, "Barrel hoops", 4f);
            }

            // 錢幣堆：16 堆，每堆是幾枚疊起來的硬幣
            for (int i = 0; i < 16; i++)
            {
                var p = Polar(R(5f, 30f), R(0f, 360f));
                var pile = new GameObject("Coins").transform;
                pile.SetParent(root, false);
                pile.position = p;
                int n = rng.Next(3, 7);
                for (int k = 0; k < n; k++)
                    Allomancy.Build.Prim(PrimitiveType.Cylinder, pile, new Vector3(R(-0.1f, 0.1f), 0.03f + k * 0.05f, R(-0.1f, 0.1f)),
                                         new Vector3(0.25f, 0.02f, 0.25f), gold);
                var coinCol = pile.gameObject.AddComponent<SphereCollider>();   // 錢幣很輕，會被推飛
                coinCol.radius = 0.15f;
                coinCol.center = new Vector3(0, 0.15f, 0);
                var coinBody = pile.gameObject.AddComponent<Rigidbody>();
                coinBody.mass = 0.3f;
                coinBody.interpolation = RigidbodyInterpolation.Interpolate;
                AddMetal(pile.gameObject, "Coins", 0.3f);
            }

            // 中央的鐵雕像（最重的金屬，藍線最粗）
            var statue = new GameObject("IronStatue").transform;
            statue.SetParent(root, false);
            Allomancy.Build.Prim(PrimitiveType.Cylinder, statue, new Vector3(0, 0.5f, 0), new Vector3(3f, 0.5f, 3f), stone, collider: true);
            var figure = new GameObject("Figure").transform;
            figure.SetParent(statue, false);
            Allomancy.Build.Prim(PrimitiveType.Capsule, figure, new Vector3(0, 2.4f, 0), new Vector3(0.9f, 1.4f, 0.9f), iron, collider: true);
            Allomancy.Build.Prim(PrimitiveType.Sphere, figure, new Vector3(0, 4.1f, 0), Vector3.one * 0.8f, iron);
            Allomancy.Build.Prim(PrimitiveType.Cube, figure, new Vector3(0.7f, 3.4f, 0), new Vector3(0.2f, 2.2f, 0.2f), iron, new Vector3(0, 0, -25f));
            AddMetal(figure.gameObject, "Iron statue", 2000f);

            // 敵人的據點：4 個，在路燈圈和石柱圈之間
            enemyHomes = new List<Vector3>();
            for (int i = 0; i < 4; i++) enemyHomes.Add(Polar(18f, i * 90f + 45f));

            // 金屬藥瓶的位置：6 個
            var vials = new List<Vector3>();
            for (int i = 0; i < 6; i++) vials.Add(Polar(9f, i * 60f) + Vector3.up * 0.8f);

            SetupLighting();
            return vials;
        }

        static void AddMetal(GameObject go, string name, float kg)
        {
            var m = go.AddComponent<MetalObject>();
            m.displayName = name;
            m.mass = kg;
        }

        static void SetupLighting()
        {
            // 黃昏、帶霧的氣氛
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.42f, 0.42f, 0.48f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.38f);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.transform.rotation = Quaternion.Euler(28f, -60f, 0f);
                l.color = new Color(1f, 0.78f, 0.6f);
                l.intensity = 0.9f;
                l.shadows = LightShadows.Soft;
            }
        }
    }

    /// 金屬藥瓶：碰到就補充所有金屬 40%，20 秒後重新出現。
    public class MetalVial : MonoBehaviour
    {
        public MetalReserves reserves;
        public Transform player;
        Vector3 basePos;
        float respawn;
        GameObject body;
        public bool Available => body.activeSelf;

        public static MetalVial Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("MetalVial");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var v = go.AddComponent<MetalVial>();
            v.basePos = pos;
            v.body = new GameObject("Body");
            v.body.transform.SetParent(go.transform, false);
            Build.Prim(PrimitiveType.Capsule, v.body.transform, Vector3.zero, new Vector3(0.22f, 0.25f, 0.22f), Build.Glow(new Color(0.7f, 0.85f, 1f)));
            Build.Prim(PrimitiveType.Cylinder, v.body.transform, new Vector3(0, 0.3f, 0), new Vector3(0.1f, 0.06f, 0.1f), Build.Mat(new Color(0.4f, 0.3f, 0.2f)));
            return v;
        }

        void Update()
        {
            if (respawn > 0f)
            {
                respawn -= Time.deltaTime;
                if (respawn <= 0f) body.SetActive(true);
                return;
            }
            transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.15f;
            transform.Rotate(0, 60f * Time.deltaTime, 0);
            if ((player.position + Vector3.up - transform.position).sqrMagnitude < 1.3f * 1.3f)
            {
                reserves.Refill(0.4f);
                SoundFx.Play(SoundFx.Kind.Pickup);
                body.SetActive(false);
                respawn = 20f;
            }
        }
    }
}
