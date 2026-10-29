using UnityEngine;

namespace FunctionSelection
{
    /// 
    /// 建立 primitive 的小工具：位置、大小、旋轉、材質一行搞定。
    /// 
    public static class Geo
    {
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale,
                                      Material mat, Vector3 localEuler = default, bool collider = false, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        public static Light PointLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
