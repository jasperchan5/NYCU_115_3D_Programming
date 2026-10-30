using System.Collections.Generic;
using UnityEngine;

namespace Allomancy
{
    /// 
    /// 燃燒鐵或鋼時：從玩家胸口拉出藍線，指向視野範圍內每個金屬物體的重心。
    /// 「視野範圍內」= 距離不超過 range，而且在主攝影機的畫面裡（viewport 座標 0~1、在鏡頭前方）。
    /// 線的粗細跟物體重量有關、越遠越細，並且有一點閃爍。
    /// 顏色：比玩家重 → 深藍（推它會把自己推開）；比玩家輕 → 淺藍（推它會把它推飛）。
    /// 正在瞄準的那條線變粗、變亮。
    /// 
    public class SteelSight : MonoBehaviour
    {
        public MetalReserves reserves;
        public Transform chest;            // 線的起點
        public Camera view;
        public float range = 35f;
        public AllomanticTargeting targeting;     // 用來找出被瞄準的那條線

        readonly List<LineRenderer> lines = new List<LineRenderer>();   // 物件池
        Material heavyMat, lightMat, targetMat;

        void Start()
        {
            heavyMat = Build.GlowTransparent(new Color(0.12f, 0.28f, 0.85f));   // 深藍
            lightMat = Build.GlowTransparent(new Color(0.55f, 0.85f, 1f));      // 淺藍
            targetMat = Build.GlowTransparent(new Color(0.85f, 0.95f, 1f));     // 被瞄準：接近白色
        }

        void LateUpdate()
        {
            int used = 0;
            bool active = reserves.IsBurning(Metal.Iron) || reserves.IsBurning(Metal.Steel);

            if (active)
            {
                var from = chest.position;
                foreach (var metal in MetalObject.All)
                {
                    var to = metal.CenterOfMass;
                    float dist = Vector3.Distance(from, to);
                    if (dist > range || dist < 0.5f) continue;

                    // 在鏡頭畫面內才畫（z > 0 代表在鏡頭前方）
                    var vp = view.WorldToViewportPoint(to);
                    if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;

                    var line = GetLine(used++);
                    bool aimed = targeting != null && targeting.Kind == TargetKind.Metal && targeting.TargetMetal == metal;
                    line.sharedMaterial = aimed ? targetMat : metal.HeavierThanPlayer ? heavyMat : lightMat;
                    float width = (0.008f + 0.04f * Mathf.Log(1f + metal.mass)) * Mathf.Lerp(1f, 0.4f, dist / range);
                    if (aimed) width *= 2f;
                    width *= 0.85f + 0.15f * Mathf.Sin(Time.time * 25f + used);        // 微微閃爍
                    line.startWidth = width;
                    line.endWidth = width * 0.6f;
                    line.SetPosition(0, from);
                    line.SetPosition(1, to);

                    // 透明度：近 50% → 遠 25%（dist 已經保證 ≤ range）
                    float alpha = Mathf.Lerp(0.5f, 0.25f, dist / range);
                    line.startColor = line.endColor = new Color(1f, 1f, 1f, alpha);
                }
            }

            for (int i = used; i < lines.Count; i++) lines[i].enabled = false;
        }

        LineRenderer GetLine(int i)
        {
            if (i == lines.Count)
            {
                var go = new GameObject("SteelLine");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lines.Add(lr);
            }
            lines[i].enabled = true;
            return lines[i];
        }
    }
}
