using UnityEngine;

namespace FunctionSelection
{

    /// 可收集的寶石：旋轉、上下浮動，玩家碰到就收集。
    public class Gem : MonoBehaviour
    {
        Vector3 basePos;
        float phase;

        public static Gem Build(Transform parent, Vector3 pos, Color color, float phase)
        {
            var go = new GameObject("Gem");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            // 兩個轉 45 度的方塊疊出八面體的樣子
            Geo.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.45f, 0.45f, 0.45f), MaterialLib.Emissive(color, 2.5f), new Vector3(45, 0, 45));
            Geo.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.32f, 0.6f, 0.32f), MaterialLib.Emissive(color * 1.2f, 2f), new Vector3(0, 45, 0));
            var g = go.AddComponent<Gem>();
            g.basePos = pos;
            g.phase = phase;
            return g;
        }

        void Update()
        {
            phase += Time.deltaTime;
            transform.position = basePos + Vector3.up * Mathf.Sin(phase * 2f) * 0.2f;
            transform.Rotate(0, 90f * Time.deltaTime, 0, Space.World);

            var player = GameApp.I != null ? GameApp.I.ActivePlayer : null;
            if (player == null) return;
            var d = player.transform.position + Vector3.up - transform.position;
            if (d.sqrMagnitude < 1.4f * 1.4f) GameApp.I.OnGemCollected(this);
        }
    }

    /// 一次性的粒子爆發（收集寶石、受傷時）。
    public static class Burst
    {
        public static void At(Vector3 pos, Color color, int count = 40, float speed = 4f)
        {
            var go = new GameObject("Burst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.7f;
            main.startSpeed = speed;
            main.startSize = 0.16f;
            main.startColor = color;
            main.gravityModifier = 0.6f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.2f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = MaterialLib.Particle(color);
            ps.Play();
        }
    }
}
