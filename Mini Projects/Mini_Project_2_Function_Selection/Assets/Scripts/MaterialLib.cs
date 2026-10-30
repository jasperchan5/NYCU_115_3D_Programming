using System.Collections.Generic;
using UnityEngine;

namespace FunctionSelection
{
    /// 
    /// 執行時建立 URP 材質並快取，避免同色材質重複建立（所有幾何都是程式生成的 primitive）。
    /// 
    public static class MaterialLib
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader lit, particle;

        static Shader Lit
        {
            get
            {
                if (lit == null)
                    lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) lit = Shader.Find("Standard");
                return lit;
            }
        }

        public static Material Get(Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            string key = $"L{ColorUtility.ToHtmlStringRGBA(color)}_{smoothness:F2}_{metallic:F2}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Lit) { name = key };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            cache[key] = m;
            return m;
        }

        /// 會發光的材質（窗戶、寶石、燈泡）。
        public static Material Emissive(Color color, float intensity = 2f)
        {
            string key = $"E{ColorUtility.ToHtmlStringRGBA(color)}_{intensity:F2}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Material(Lit) { name = key };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", color * intensity);
            cache[key] = m;
            return m;
        }

        /// 粒子用的材質（噴泉水花、螢火蟲）。
        public static Material Particle(Color color)
        {
            string key = $"P{ColorUtility.ToHtmlStringRGBA(color)}";
            if (cache.TryGetValue(key, out var m)) return m;
            if (particle == null)
                particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particle == null)
                particle = Shader.Find("Sprites/Default");
            m = new Material(particle) { name = key };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            cache[key] = m;
            return m;
        }
    }
}
