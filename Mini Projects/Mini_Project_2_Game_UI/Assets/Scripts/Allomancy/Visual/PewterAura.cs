using System.Collections.Generic;
using UnityEngine;

namespace Allomancy
{
    /// 
    /// 燃燒白鑞時：角色模型外圍環繞一圈淡紅色光暈。
    ///
    /// 做法（外殼法）：把玩家模型的每個零件複製一份，稍微放大，套上半透明淡紅色材質。
    /// 外殼比模型大一點 → 在模型輪廓外面露出一圈紅色的邊 = 光暈。
    /// 用兩層外殼（內層 4 cm 較濃、外層 10 cm 較淡）讓光暈由內往外變淡。
    ///
    /// 放大的方式：每個軸各自算縮放倍率，讓每個零件「往外長出固定的厚度」，
    /// 而不是統一乘 1.1（那樣細長的零件會變形，粗的零件光暈太厚）：
    ///     size  = mesh 大小 × 物件縮放（世界尺寸）
    ///     scale = 1 + 2 × 厚度 / size        （兩邊各長出一個厚度）
    /// 另外加一盞紅色點光源，讓地面跟周圍也被染紅一點；光暈透明度會跟著「心跳」起伏。
    /// 
    public class PewterAura : MonoBehaviour
    {
        MetalReserves reserves;
        readonly List<GameObject> shells = new List<GameObject>();
        Material innerMat, outerMat;
        Light glow;
        float fade;

        static readonly Color AuraColor = new Color(1f, 0.35f, 0.3f);     // 淡紅色
        const float InnerThickness = 0.04f, OuterThickness = 0.10f;
        const float InnerAlpha = 0.35f, OuterAlpha = 0.15f;

        /// 要在其他特效（例如紅銅雲）建立之前呼叫，才不會連雲也包進光暈。
        public static PewterAura Create(PlayerController player, MetalReserves reserves)
        {
            var a = player.gameObject.AddComponent<PewterAura>();
            a.reserves = reserves;
            a.innerMat = new Material(Build.GlowTransparent(AuraColor));
            a.outerMat = new Material(Build.GlowTransparent(AuraColor));

            // 先把模型零件收集起來，再加外殼（不然迴圈會把剛加的外殼也算進去）
            var parts = player.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in parts)
            {
                a.AddShell(mf, InnerThickness, a.innerMat);
                a.AddShell(mf, OuterThickness, a.outerMat);
            }

            a.glow = Build.PointLight(player.Chest, Vector3.zero, AuraColor, 3.5f, 0f);
            a.SetVisible(false);
            return a;
        }

        void AddShell(MeshFilter part, float thickness, Material mat)
        {
            var shell = new GameObject("PewterShell");
            shell.transform.SetParent(part.transform, false);       // 跟著零件動（披風擺動時光暈也跟著擺）
            shell.AddComponent<MeshFilter>().sharedMesh = part.sharedMesh;
            var r = shell.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var bounds = part.sharedMesh.bounds;
            var world = part.transform.lossyScale;
            var scale = new Vector3(
                1f + 2f * thickness / Mathf.Max(0.01f, bounds.size.x * world.x),
                1f + 2f * thickness / Mathf.Max(0.01f, bounds.size.y * world.y),
                1f + 2f * thickness / Mathf.Max(0.01f, bounds.size.z * world.z));
            shell.transform.localScale = scale;
            // 縮放是以原點為中心；要讓 mesh 的中心 c 不動：位置 = c − scale × c
            // （Unity 內建 primitive 的中心都在原點，c = 0，所以位置就是 0）
            shell.transform.localPosition = bounds.center - Vector3.Scale(scale, bounds.center);
            shells.Add(shell);
        }

        void SetVisible(bool on)
        {
            foreach (var s in shells) if (s.activeSelf != on) s.SetActive(on);
            glow.enabled = on;
        }

        void Update()
        {
            bool on = reserves.IsBurning(Metal.Pewter);
            fade = Mathf.MoveTowards(fade, on ? 1f : 0f, Time.deltaTime * 3f);
            SetVisible(fade > 0.001f);
            if (fade <= 0.001f) return;

            // 心跳：|sin|^4 做出一下一下的脈動
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * 2.2f)), 4f);
            float k = fade * (0.75f + 0.25f * beat);
            innerMat.color = new Color(AuraColor.r, AuraColor.g, AuraColor.b, InnerAlpha * k);
            outerMat.color = new Color(AuraColor.r, AuraColor.g, AuraColor.b, OuterAlpha * k);
            glow.intensity = 1.2f * k;
        }
    }
}
