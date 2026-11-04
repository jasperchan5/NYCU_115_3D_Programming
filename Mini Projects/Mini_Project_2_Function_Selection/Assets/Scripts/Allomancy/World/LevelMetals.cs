using System.Collections.Generic;
using UnityEngine;

namespace Allomancy
{
    /// 
    /// 把關卡裡的東西變成鎔金術用得到的金屬（每次開始遊戲時做一次，回主選單時由 AllomancySession 收掉）。
    /// 關卡（LevelBuilder）只負責蓋外形並取名字，這裡依名字決定重量：
    ///   Lamp        鐵路燈        150 公斤，固定
    ///   BridgePole  對岸的鐵橋柱  120 公斤，固定，有落腳點（拉它 → 飛過裂谷）
    ///   WallNail    城牆上的鐵釘   15 公斤，固定（燒鐵時藍線指向的那些釘子）
    ///   WallBeacon  牆頂的烽火柱  300 公斤，固定，有落腳點（拉它 → 飛上牆頂）
    /// 另外放上每次都要重新擺好的東西：
    ///   BarrierSpot     → 帶鐵刺的木柵欄（有剛體，29 公斤，比玩家輕 → 鋼推會把它推飛）
    ///   VialSpot_Iron   → 鐵藥瓶；VialSpot_Steel → 鋼藥瓶（喝下溶液之前先藏起來）
    /// 
    public static class LevelMetals
    {
        public class Layout
        {
            /// 掛在關卡原有物件上的 MetalObject；遊戲結束時要拔掉，關卡才會恢復原狀。
            public readonly List<MetalObject> attachedToLevel = new List<MetalObject>();
            public readonly List<Rigidbody> barriers = new List<Rigidbody>();
            public Transform vials;
        }

        public static Layout Build(Transform sessionRoot, Transform levelRoot, MetalReserves reserves, Transform player)
        {
            var layout = new Layout();
            layout.vials = new GameObject("Vials").transform;
            layout.vials.SetParent(sessionRoot, false);

            // 找出關卡裡所有有名字的物件（包含子物件：鐵釘、烽火柱在 Wall 底下）
            foreach (var t in levelRoot.GetComponentsInChildren<Transform>())
            {
                switch (t.name)
                {
                    case "Lamp": Attach(layout, t, "Iron lamp post", 150f); break;
                    case "BridgePole": Attach(layout, t, "Bridge pole", 120f); break;
                    case "WallNail": Attach(layout, t, "Iron nail", 15f); break;
                    case "WallBeacon": Attach(layout, t, "Iron beacon", 300f); break;
                    case "BarrierSpot": layout.barriers.Add(BuildBarrier(sessionRoot, t.position)); break;
                    case "VialSpot_Iron": CreateVial(layout, t.position, Metal.Iron, reserves, player); break;
                    case "VialSpot_Steel": CreateVial(layout, t.position, Metal.Steel, reserves, player); break;
                }
            }
            layout.vials.gameObject.SetActive(false);
            return layout;
        }

        static void Attach(Layout layout, Transform t, string name, float kg)
        {
            var m = t.gameObject.AddComponent<MetalObject>();
            m.displayName = name;
            m.mass = kg;
            m.landing = t.Find("Landing");                   // 沒有這個子物件就是 null
            layout.attachedToLevel.Add(m);
        }

        static void CreateVial(Layout layout, Vector3 pos, Metal metal, MetalReserves reserves, Transform player)
        {
            var v = MetalVial.Create(layout.vials, pos, metal);
            v.reserves = reserves;
            v.player = player;
            ScentTrail.Attach(v, reserves, player);
        }

        /// 
        /// 帶鐵刺的木柵欄：兩根腳（底下有橫木當腳架）、三片橫板，正面一排鐵刺朝向南方（玩家來的方向）。
        /// 只有鐵刺是金屬；整個柵欄是一個剛體，重量 = 木頭 25 + 鐵刺 4 公斤。
        /// 重心設低一點，平常才不會自己倒下。
        /// 
        static Rigidbody BuildBarrier(Transform parent, Vector3 pos)
        {
            var wood = Allomancy.Build.Mat(new Color(0.42f, 0.29f, 0.17f));
            var iron = Allomancy.Build.Mat(new Color(0.3f, 0.3f, 0.33f), 0.6f, 0.9f);
            var root = new GameObject("SpikeBarrier").transform;
            root.SetParent(parent, false);
            root.position = pos;

            foreach (float x in new[] { -1.45f, 1.45f })
            {
                Allomancy.Build.Prim(PrimitiveType.Cube, root, new Vector3(x, 0.95f, 0), new Vector3(0.25f, 1.9f, 0.25f), wood, collider: true);
                Allomancy.Build.Prim(PrimitiveType.Cube, root, new Vector3(x, 0.1f, 0), new Vector3(0.3f, 0.2f, 1.4f), wood, collider: true);   // 腳架
            }
            foreach (float y in new[] { 0.6f, 1.1f, 1.6f })
                Allomancy.Build.Prim(PrimitiveType.Cube, root, new Vector3(0, y, 0), new Vector3(3.4f, 0.3f, 0.12f), wood, collider: true);

            var spikes = new GameObject("Spikes").transform;
            spikes.SetParent(root, false);
            for (int i = 0; i < 7; i++)
                Allomancy.Build.Prim(PrimitiveType.Cylinder, spikes, new Vector3(-1.5f + i * 0.5f, 1.1f, -0.45f), new Vector3(0.07f, 0.4f, 0.07f), iron, new Vector3(90f, 0, 0));

            var rb = root.gameObject.AddComponent<Rigidbody>();   // 先加剛體，MetalObject 才找得到
            rb.mass = 25f + 4f;
            rb.centerOfMass = new Vector3(0f, 0.4f, 0f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var m = spikes.gameObject.AddComponent<MetalObject>();
            m.displayName = "Barrier spikes";
            m.mass = 4f;
            return rb;
        }
    }
}
