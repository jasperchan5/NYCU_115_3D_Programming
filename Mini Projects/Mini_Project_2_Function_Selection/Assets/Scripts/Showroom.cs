using System.Collections.Generic;
using UnityEngine;

namespace FunctionSelection
{
    /// 
    /// 選角展示間：每個角色站在自己的展示台上，被選到的角色會轉身、揮手，展示台發光、聚光燈變亮。
    /// 放在遠離村莊的位置（x = 400），攝影機切過來時才看得到。
    /// 
    public class Showroom : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(400f, 0f, 0f);
        const float Spacing = 3.4f;

        readonly List<CharacterRig> rigs = new List<CharacterRig>();
        readonly List<Renderer> rings = new List<Renderer>();
        readonly List<Light> spots = new List<Light>();
        int selected;
        float spin;

        public int Count => rigs.Count;

        public static Showroom Build(Transform parent)
        {
            var root = new GameObject("Showroom");
            root.transform.SetParent(parent, false);
            root.transform.position = Origin;
            var room = root.AddComponent<Showroom>();

            // 地板與半圓形的背牆
            Geo.Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, -0.05f, 2f), new Vector3(26f, 0.05f, 26f),
                     MaterialLib.Get(new Color(0.12f, 0.12f, 0.16f), 0.75f), name: "Floor");
            for (int i = 0; i < 24; i++)
            {
                float a = Mathf.Lerp(-80f, 80f, i / 23f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 11f, 6f, 2f + Mathf.Cos(a) * 11f);
                var col = Color.Lerp(new Color(0.18f, 0.2f, 0.32f), new Color(0.28f, 0.18f, 0.32f), i / 23f);
                var panel = Geo.Prim(PrimitiveType.Cube, root.transform, p, new Vector3(1.5f, 12f, 0.3f), MaterialLib.Get(col, 0.4f), name: "WallPanel");
                panel.transform.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            }

            var defs = CharacterCatalog.All;
            for (int i = 0; i < defs.Length; i++)
            {
                float x = (i - (defs.Length - 1) / 2f) * Spacing;
                var basePos = new Vector3(x, 0f, 0f);
                Geo.Prim(PrimitiveType.Cylinder, root.transform, basePos + Vector3.up * 0.2f, new Vector3(2.2f, 0.2f, 2.2f),
                         MaterialLib.Get(new Color(0.3f, 0.3f, 0.36f), 0.7f, 0.4f), name: "Pedestal");
                var ring = Geo.Prim(PrimitiveType.Cylinder, root.transform, basePos + Vector3.up * 0.41f, new Vector3(2.35f, 0.01f, 2.35f),
                                    MaterialLib.Emissive(defs[i].accent, 2f), name: "GlowRing");
                room.rings.Add(ring.GetComponent<Renderer>());

                var rig = CharacterBuilder.Build(defs[i], root.transform, "Preview_" + defs[i].name);
                rig.transform.localPosition = basePos + Vector3.up * 0.42f;
                rig.transform.localRotation = Quaternion.Euler(0, 180f, 0);   // 面向鏡頭（鏡頭在 -z）
                room.rigs.Add(rig);

                var spotGo = new GameObject("Spot");
                spotGo.transform.SetParent(root.transform, false);
                spotGo.transform.localPosition = basePos + new Vector3(0, 6f, -2f);
                spotGo.transform.LookAt(spotGo.transform.parent.TransformPoint(basePos + Vector3.up));
                var spot = spotGo.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.spotAngle = 38f;
                spot.range = 12f;
                spot.color = Color.Lerp(Color.white, defs[i].accent, 0.3f);
                spot.shadows = LightShadows.Soft;
                room.spots.Add(spot);
            }
            room.Select(CharacterCatalog.SelectedIndex);
            return room;
        }

        public void Select(int index)
        {
            selected = (index % rigs.Count + rigs.Count) % rigs.Count;
            rigs[selected].wave = 1.2f;    // 被選到時揮手打招呼
            spin = 0f;
        }

        public int Selected => selected;

        /// 攝影機要放的位置：對準目前選到的角色，稍微偏向它。
        public void CameraPose(out Vector3 pos, out Vector3 look)
        {
            var target = rigs[selected].transform.position;
            pos = Origin + new Vector3(target.x - Origin.x, 0, 0) * 0.55f + new Vector3(0, 2.3f, -7.2f);
            look = target + Vector3.up * 1.3f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            spin += dt * 40f;
            for (int i = 0; i < rigs.Count; i++)
            {
                bool on = i == selected;
                // 選到的角色在台上慢慢轉；其他角色轉回來面向鏡頭
                var want = on ? Quaternion.Euler(0, 180f + Mathf.Sin(spin * Mathf.Deg2Rad) * 50f, 0) : Quaternion.Euler(0, 180f, 0);
                rigs[i].transform.localRotation = Quaternion.Slerp(rigs[i].transform.localRotation, want, 1f - Mathf.Exp(-5f * dt));
                spots[i].intensity = Mathf.Lerp(spots[i].intensity, on ? 8f : 1.2f, 1f - Mathf.Exp(-6f * dt));
                rings[i].enabled = on;
                rigs[i].SetLocomotion(0f, true);
            }
        }
    }
}
