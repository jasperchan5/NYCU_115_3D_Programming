using System.Collections.Generic;
using UnityEngine;

namespace Allomancy
{
    ///
    /// 把村莊變成鎔金術能用的場地（每次開始遊戲時做一次，回主選單時由 AllomancySession 收掉）：
    ///   1. 村莊原本的 12 盞路燈 → 掛上 MetalObject，變成 150 公斤、固定在地上的鐵路燈
    ///   2. 用迴圈加上會被推動的金屬：
    ///        每間房屋旁一個鐵箍木桶（6 個，29 公斤，有剛體）
    ///        廣場上 16 堆錢幣（0.3 公斤，有剛體，一推就飛）
    ///   3. 回傳士兵的據點（4 個，在房屋之間的空地）與金屬藥瓶的位置（6 個，廣場一圈）
    /// 用固定的亂數種子，每次擺放的位置都一樣。
    ///
    public static class VillageMetals
    {
        public class Layout
        {
            public readonly List<Vector3> soldierHomes = new List<Vector3>();
            public readonly List<Vector3> vialPoints = new List<Vector3>();
            /// 掛在村莊原有物件（路燈）上的 MetalObject；遊戲結束時要拔掉，村莊才會恢復原狀。
            public readonly List<MetalObject> attachedToLevel = new List<MetalObject>();
        }

        // 士兵據點的角度：房屋在 60°、120°、180°…（每 60 度一間），士兵放在兩間房屋中間；
        // 90° 是通往山洞的步道，留空
        static readonly float[] SoldierAngles = { 30f, 150f, 210f, 330f };
        const float SoldierRadius = 18f;

        public static Layout Build(Transform sessionRoot, Transform levelRoot)
        {
            var layout = new Layout();
            var rng = new System.Random(11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var iron = Allomancy.Build.Mat(new Color(0.3f, 0.3f, 0.33f), 0.6f, 0.9f);
            var wood = Allomancy.Build.Mat(new Color(0.45f, 0.3f, 0.18f));
            var gold = Allomancy.Build.Mat(new Color(0.85f, 0.7f, 0.3f), 0.8f, 1f);

            // 1. 原有的路燈、2a. 房屋旁的鐵箍木桶：找村莊裡名字是 "Lamp" / "House" 的物件
            foreach (Transform child in levelRoot)
            {
                if (child.name == "Lamp")
                {
                    layout.attachedToLevel.Add(AddMetal(child.gameObject, "Iron lamp post", 150f));
                }
                else if (child.name == "House")
                {
                    // 房屋的方向角，木桶放在同一圈稍微偏旁邊（避開門口的石板路）
                    float ang = Mathf.Atan2(child.position.z, child.position.x) * Mathf.Rad2Deg;
                    BuildBarrel(sessionRoot, Polar(20f, ang + 14f), wood, iron);
                }
            }

            // 2b. 廣場上的錢幣堆：16 堆，繞著噴泉一圈
            for (int i = 0; i < 16; i++)
                BuildCoins(sessionRoot, Polar(R(6.5f, 10.5f), i * 22.5f + R(-6f, 6f)), gold, rng);

            // 3. 士兵據點與藥瓶
            foreach (float a in SoldierAngles) layout.soldierHomes.Add(Polar(SoldierRadius, a));
            for (int i = 0; i < 6; i++) layout.vialPoints.Add(Polar(9f, i * 60f + 30f) + Vector3.up * 0.8f);
            return layout;
        }

        /// 鐵箍木桶：木頭桶身 + 兩圈鐵箍（只有鐵箍是金屬）。剛體的重量 = 木頭 25 + 鐵箍 4。
        static void BuildBarrel(Transform parent, Vector3 pos, Material wood, Material iron)
        {
            var barrel = new GameObject("IronBarrel").transform;
            barrel.SetParent(parent, false);
            barrel.position = pos;
            Allomancy.Build.Prim(PrimitiveType.Cylinder, barrel, new Vector3(0, 0.6f, 0), new Vector3(0.8f, 0.6f, 0.8f), wood, collider: true);
            var rb = barrel.gameObject.AddComponent<Rigidbody>();      // 先加剛體，MetalObject 才找得到
            rb.mass = 25f + 4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var hoops = new GameObject("Hoops").transform;
            hoops.SetParent(barrel, false);
            foreach (float y in new[] { 0.25f, 0.95f })
                Allomancy.Build.Prim(PrimitiveType.Cylinder, hoops, new Vector3(0, y, 0), new Vector3(0.84f, 0.04f, 0.84f), iron);
            AddMetal(hoops.gameObject, "Barrel hoops", 4f);
        }

        /// 錢幣堆：3–6 枚疊起來的硬幣，一個球形碰撞體 + 很輕的剛體。
        static void BuildCoins(Transform parent, Vector3 pos, Material gold, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var pile = new GameObject("Coins").transform;
            pile.SetParent(parent, false);
            pile.position = pos;
            int n = rng.Next(3, 7);
            for (int k = 0; k < n; k++)
                Allomancy.Build.Prim(PrimitiveType.Cylinder, pile, new Vector3(R(-0.1f, 0.1f), 0.03f + k * 0.05f, R(-0.1f, 0.1f)),
                                     new Vector3(0.25f, 0.02f, 0.25f), gold);
            var col = pile.gameObject.AddComponent<SphereCollider>();
            col.radius = 0.15f;
            col.center = new Vector3(0, 0.15f, 0);
            var body = pile.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            AddMetal(pile.gameObject, "Coins", 0.3f);
        }

        static MetalObject AddMetal(GameObject go, string name, float kg)
        {
            var m = go.AddComponent<MetalObject>();
            m.displayName = name;
            m.mass = kg;
            return m;
        }

        /// 極座標 → 位置（0° = +x，90° = +z 北方），跟村莊的 LevelBuilder.Polar 一樣。
        static Vector3 Polar(float r, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
    }
}
