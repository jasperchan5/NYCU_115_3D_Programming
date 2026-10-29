using UnityEngine;

namespace FunctionSelection
{
    /// 
    /// 史萊姆怪物：在出沒點附近跳來跳去，玩家靠近時追過去，撞到玩家會把他彈開並扣血。
    /// 跳躍用 |sin| 做高度、用壓扁 / 拉長做彈性（squash &amp; stretch）。
    /// 
    public class Monster : MonoBehaviour
    {
        public Vector3 home;
        public float wanderRadius = 7f;
        public float chaseRadius = 9f;
        public Transform body;

        float speed, hopPhase, hitCooldown, retargetTimer;
        Vector3 wanderTarget;

        public static Monster Build(Transform parent, Vector3 home, Color color, System.Random rng)
        {
            var go = new GameObject("Slime");
            go.transform.SetParent(parent, false);
            go.transform.position = home;
            var m = go.AddComponent<Monster>();
            m.home = home;
            m.speed = 2.2f + (float)rng.NextDouble() * 1.2f;
            m.hopPhase = (float)rng.NextDouble() * 10f;

            m.body = Geo.Pivot("Body", go.transform, Vector3.zero);
            Geo.Prim(PrimitiveType.Sphere, m.body, new Vector3(0, 0.55f, 0), new Vector3(1.3f, 1.05f, 1.3f), MaterialLib.Get(color, 0.85f));
            var white = MaterialLib.Get(Color.white, 0.6f);
            var black = MaterialLib.Get(new Color(0.05f, 0.05f, 0.08f), 0.8f);
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                Geo.Prim(PrimitiveType.Sphere, m.body, new Vector3(x, 0.75f, 0.52f), Vector3.one * 0.26f, white, name: "Eye");
                Geo.Prim(PrimitiveType.Sphere, m.body, new Vector3(x, 0.75f, 0.63f), Vector3.one * 0.12f, black, name: "Pupil");
            }
            // 頭頂的小角，讓怪物比較有個性
            Geo.Prim(PrimitiveType.Capsule, m.body, new Vector3(0, 1.12f, 0), new Vector3(0.12f, 0.15f, 0.12f), MaterialLib.Get(color * 0.7f, 0.6f));
            m.PickWanderTarget();
            return m;
        }

        void PickWanderTarget()
        {
            var r = Random.insideUnitCircle * wanderRadius;
            wanderTarget = home + new Vector3(r.x, 0, r.y);
            retargetTimer = Random.Range(2f, 5f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            hitCooldown -= dt;
            retargetTimer -= dt;

            var player = GameApp.I != null ? GameApp.I.ActivePlayer : null;
            Vector3 goal = wanderTarget;
            float spd = speed;
            bool chasing = false;

            if (player != null)
            {
                var toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0f;
                float dist = toPlayer.magnitude;
                if (dist < chaseRadius && (player.transform.position - home).magnitude < wanderRadius + chaseRadius)
                {
                    goal = player.transform.position;
                    spd = speed * 1.5f;
                    chasing = true;
                }
                if (dist < 1.3f && hitCooldown <= 0f)
                {
                    hitCooldown = 1.2f;
                    GameApp.I.OnPlayerHit(this);
                }
            }

            var to = goal - transform.position;
            to.y = 0f;
            if (!chasing && (to.magnitude < 0.5f || retargetTimer <= 0f)) PickWanderTarget();

            // 跳躍節奏：追人時跳得比較快
            hopPhase += dt * (chasing ? 9f : 6f);
            float hop = Mathf.Abs(Mathf.Sin(hopPhase));
            bool airborne = hop > 0.15f;

            if (to.sqrMagnitude > 0.01f)
            {
                var dir = to.normalized;
                if (airborne) transform.position += dir * spd * dt;      // 只在空中前進，落地時停一下
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-6f * dt));
            }

            // 高度與 squash & stretch
            float h = hop * (chasing ? 0.9f : 0.6f);
            body.localPosition = new Vector3(0, h, 0);
            float stretch = airborne ? 1f + 0.18f * hop : 1f - 0.25f * (1f - hop / 0.15f);
            body.localScale = new Vector3(1f / Mathf.Sqrt(stretch), stretch, 1f / Mathf.Sqrt(stretch));
        }
    }

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
