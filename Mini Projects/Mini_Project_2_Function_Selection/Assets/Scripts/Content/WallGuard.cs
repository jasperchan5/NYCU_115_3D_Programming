using UnityEngine;

namespace FunctionSelection
{
    ///
    /// 城牆上巡邏的士兵：在牆頂的步道上來回走（A 點 ↔ B 點），走到端點停一下、轉身再走回去。
    /// 外形直接用選角的 CharacterBuilder 組出來（戴頭盔、拿長矛），走路動畫也是同一套 CharacterRig。
    /// 只是背景，不會攻擊玩家，也不是金屬目標。
    ///
    public class WallGuard : MonoBehaviour
    {
        Vector3 a, b;                 // 巡邏的兩個端點
        bool toB = true;
        float wait;                   // 在端點停留的剩餘秒數
        CharacterRig rig;

        const float Speed = 1.6f, WaitTime = 1.5f;

        static readonly CharacterDef GuardDef = new CharacterDef
        {
            name = "Guard", title = "", description = "",
            body = new Color(0.32f, 0.33f, 0.38f), accent = new Color(0.55f, 0.5f, 0.42f),
            skin = new Color(0.9f, 0.76f, 0.64f), headgear = Headgear.Helmet,
            walkSpeed = Speed, runSpeed = Speed, jumpHeight = 0f, scale = 1.0f, bulk = 1.15f,
        };

        public static WallGuard Build(Transform parent, Vector3 from, Vector3 to, float startOffset)
        {
            var rig = CharacterBuilder.Build(GuardDef, parent, "WallGuard");
            // 右手拿一支長矛：木柄 + 矛頭
            var spear = Geo.Pivot("Spear", rig.armR, new Vector3(0f, -0.6f, 0.05f));
            Geo.Prim(PrimitiveType.Cylinder, spear, new Vector3(0f, 0.5f, 0f), new Vector3(0.06f, 1.2f, 0.06f), MaterialLib.Get(new Color(0.4f, 0.28f, 0.16f)));
            Geo.Prim(PrimitiveType.Cylinder, spear, new Vector3(0f, 1.8f, 0f), new Vector3(0.12f, 0.15f, 0.12f), MaterialLib.Get(new Color(0.7f, 0.72f, 0.75f), 0.7f, 0.8f));

            var g = rig.gameObject.AddComponent<WallGuard>();
            g.rig = rig;
            g.a = from;
            g.b = to;
            rig.transform.position = Vector3.Lerp(from, to, startOffset);   // 每個人起點不同，才不會走得一模一樣
            return g;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (wait > 0f)
            {
                wait -= dt;
                rig.SetLocomotion(0f, true);                 // 停下來：呼吸動作
                return;
            }

            var target = toB ? b : a;
            var to = target - transform.position;
            if (to.magnitude < 0.05f)
            {
                toB = !toB;                                  // 到端點：轉身，停一下
                wait = WaitTime;
                return;
            }
            var dir = to.normalized;
            transform.position = Vector3.MoveTowards(transform.position, target, Speed * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-8f * dt));
            rig.SetLocomotion(0.4f, true);                   // 0.4 ≈ 慢慢走
        }
    }
}