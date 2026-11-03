using System.Collections.Generic;
using UnityEngine;

namespace Allomancy
{
    /// 
    /// 拿劍的士兵：平常繞著自己的據點巡邏，看到玩家就追過去揮劍。
    /// 劍和頭盔是金屬（MetalObject），燒鐵 / 鋼時藍線會指過來。
    /// 胸口有「情緒核心」，只有燒鋅或黃銅時才看得到：
    ///   平靜 → 黃色、慢慢跳動；生氣（追擊中）→ 橘紅色、跳得很快。
    /// 鎔金術對他的影響：
    ///   鋅「煽動」→ 暴怒 8 秒：追擊範圍變大、跑得更快
    ///   黃銅「安撫」→ 平靜 8 秒：不追、不攻擊，回去巡邏
    ///   鋼推 / 鐵拉他的劍或頭盔 → 整個人被推開 / 拉過來（Knockback）
    /// 
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();
        public static event System.Action<Enemy> Stepped;

        public Transform player;
        public PlayerHealth playerHealth;
        public MetalReserves playerReserves;

        Vector3 home;
        float patrolRadius, patrolAngle, attackTimer, swing, anger, walkPhase;
        int lastStep;
        float riotTimer, sootheTimer;                // 被煽動 / 被安撫的剩餘秒數
        Vector3 knockVelocity;                       // 被推拉時的速度，會慢慢衰減
        Transform swordArm, legL, legR;
        Transform core;
        Renderer coreRenderer;
        readonly List<Transform> orbiters = new List<Transform>();

        const float ChaseRange = 12f, AttackRange = 2.2f, Speed = 2.6f, Damage = 12f, AttackCooldown = 1.8f;
        static readonly Color Calm = new Color(1f, 0.85f, 0.2f);
        static readonly Color Angry = new Color(1f, 0.35f, 0.05f);

        public Vector3 CorePosition => core.position;
        public bool Rioting => riotTimer > 0f;
        public bool Soothed => sootheTimer > 0f;
        public float bodyMass = 75f;

        public float TotalMass
        {
            get
            {
                float m = bodyMass;
                foreach (var gear in GetComponentsInChildren<MetalObject>()) m += gear.mass;
                return m;
            }
        }
        public Renderer[] Renderers { get; private set; }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static Enemy Create(Transform parent, Vector3 home, float patrolRadius, float startAngle)
        {
            var root = new GameObject("Soldier").transform;
            root.SetParent(parent, false);
            var e = root.gameObject.AddComponent<Enemy>();
            e.home = home;
            e.patrolRadius = patrolRadius;
            e.patrolAngle = startAngle;
            root.position = home + new Vector3(Mathf.Cos(startAngle), 0, Mathf.Sin(startAngle)) * patrolRadius;

            var cloth = Build.Mat(new Color(0.35f, 0.18f, 0.15f));
            var armor = Build.Mat(new Color(0.55f, 0.57f, 0.6f), 0.7f, 0.9f);
            var skin = Build.Mat(new Color(0.9f, 0.75f, 0.62f));

            // 身體、頭、頭盔
            Build.Prim(PrimitiveType.Capsule, root, new Vector3(0, 1.35f, 0), new Vector3(0.6f, 0.5f, 0.4f), cloth);
            Build.Prim(PrimitiveType.Sphere, root, new Vector3(0, 2.1f, 0), Vector3.one * 0.42f, skin);
            var helmet = Build.Prim(PrimitiveType.Sphere, root, new Vector3(0, 2.2f, 0), new Vector3(0.48f, 0.32f, 0.48f), armor, name: "Helmet");
            var hm = helmet.AddComponent<MetalObject>();
            hm.displayName = "Soldier's helmet";
            hm.mass = 2f;

            // 腿（關節在髖部）
            e.legL = Pivot(root, new Vector3(-0.15f, 0.9f, 0));
            e.legR = Pivot(root, new Vector3(0.15f, 0.9f, 0));
            foreach (var leg in new[] { e.legL, e.legR })
                Build.Prim(PrimitiveType.Capsule, leg, new Vector3(0, -0.45f, 0), new Vector3(0.22f, 0.45f, 0.22f), cloth);

            // 拿劍的右手：關節在肩膀，揮劍時繞 x 軸轉
            e.swordArm = Pivot(root, new Vector3(0.42f, 1.7f, 0));
            Build.Prim(PrimitiveType.Capsule, e.swordArm, new Vector3(0, -0.3f, 0), new Vector3(0.16f, 0.32f, 0.16f), cloth);
            var sword = new GameObject("Sword").transform;
            sword.SetParent(e.swordArm, false);
            sword.localPosition = new Vector3(0, -0.6f, 0.1f);
            sword.localRotation = Quaternion.Euler(70f, 0, 0);
            Build.Prim(PrimitiveType.Cube, sword, new Vector3(0, 0.6f, 0), new Vector3(0.06f, 1.2f, 0.02f), armor, name: "Blade");
            Build.Prim(PrimitiveType.Cube, sword, Vector3.zero, new Vector3(0.3f, 0.05f, 0.06f), armor, name: "Guard");
            var sm = sword.gameObject.AddComponent<MetalObject>();
            sm.displayName = "Soldier's sword";
            sm.mass = 1.5f;

            // 情緒核心：胸口的發光球，外面繞著三顆小光點
            e.core = Pivot(root, new Vector3(0, 1.45f, 0.18f));
            var coreGo = Build.Prim(PrimitiveType.Sphere, e.core, Vector3.zero, Vector3.one * 0.22f, Build.GlowTransparent(Calm), name: "EmotionCore");
            e.coreRenderer = coreGo.GetComponent<Renderer>();
            e.coreRenderer.sharedMaterial = new Material(e.coreRenderer.sharedMaterial);   // 每隻敵人自己的顏色
            for (int i = 0; i < 3; i++)
            {
                var o = Build.Prim(PrimitiveType.Sphere, e.core, Vector3.zero, Vector3.one * 0.06f, e.coreRenderer.sharedMaterial, name: "Spark");
                e.orbiters.Add(o.transform);
            }
            e.core.gameObject.SetActive(false);
            e.Renderers = root.GetComponentsInChildren<Renderer>(true);   // true：連隱藏中的情緒核心也算進去
            return e;
        }

        static Transform Pivot(Transform parent, Vector3 pos)
        {
            var t = new GameObject("Joint").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            riotTimer -= dt;
            sootheTimer -= dt;
            float chaseRange = Rioting ? ChaseRange * 1.8f : ChaseRange;   // 被煽動：更遠就會追過來
            var allomancy = GetComponent<EnemyAllomancy>();
            bool alerted = allomancy != null && allomancy.Alerted;
            bool chasing = (dist < chaseRange || alerted) && playerHealth.current > 0f && !Soothed;

            Vector3 move = Vector3.zero;
            if (chasing)
            {
                if (dist > AttackRange * 0.8f) move = toPlayer.normalized * Speed * (Rioting ? 1.8f : 1.3f);
                Face(toPlayer, dt);
                attackTimer -= dt;
                if (dist < AttackRange && attackTimer <= 0f)
                {
                    attackTimer = AttackCooldown;
                    swing = 1f;                             // 開始揮劍動畫
                    playerHealth.Damage(Damage);
                }
            }
            else
            {
                // 繞著據點巡邏：沿圓周慢慢走
                patrolAngle += dt * Speed * 0.5f / patrolRadius;
                var target = home + new Vector3(Mathf.Cos(patrolAngle), 0, Mathf.Sin(patrolAngle)) * patrolRadius;
                var to = target - transform.position;
                to.y = 0f;
                if (to.magnitude > 0.1f) { move = to.normalized * Speed * 0.5f; Face(to, dt); }
            }
            transform.position += (move + knockVelocity) * dt;
            knockVelocity = Vector3.MoveTowards(knockVelocity, Vector3.zero, 12f * dt);   // 被推開後很快停下

            // 走路：腿前後擺
            walkPhase += dt * move.magnitude * 3f;
            float leg = Mathf.Sin(walkPhase) * 35f * Mathf.Clamp01(move.magnitude);
            legL.localRotation = Quaternion.Euler(leg, 0, 0);
            legR.localRotation = Quaternion.Euler(-leg, 0, 0);
            
            int step = Mathf.FloorToInt(walkPhase / Mathf.PI);
            if (step != lastStep) { lastStep = step; Stepped?.Invoke(this); }

            // 揮劍：手臂從舉高（-120°）快速砍下
            swing = Mathf.MoveTowards(swing, 0f, dt * 2.5f);
            float armAngle = swing > 0f ? Mathf.Lerp(30f, -120f, swing) : -20f;
            swordArm.localRotation = Quaternion.Euler(armAngle, 0, 0);

            UpdateEmotionCore(chasing, dt);
        }

        void Face(Vector3 dir, float dt)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-6f * dt));
        }

        /// 鋅：煽動情緒。
        public void Riot()
        {
            riotTimer = 8f;
            sootheTimer = 0f;
            anger = Mathf.Max(anger, 0.8f);
        }

        /// 黃銅：安撫情緒。
        public void Soothe()
        {
            sootheTimer = 8f;
            riotTimer = 0f;
            anger = Mathf.Min(anger, 0.2f);
        }

        /// 被鋼推 / 鐵拉身上的金屬：整個人跟著被推開 / 拉過來（只算水平方向）。
        public void Knockback(Vector3 velocity)
        {
            velocity.y = 0f;
            knockVelocity += velocity;
        }

        void UpdateEmotionCore(bool chasing, float dt)
        {
            // 情緒值 0（平靜）~ 1（憤怒），平滑變化；被煽動時直接衝到 1，被安撫時降到 0
            float target = Rioting ? 1f : Soothed ? 0f : chasing ? 0.75f : 0.1f;
            anger = Mathf.MoveTowards(anger, target, dt * 0.8f);

            bool visible = playerReserves.IsBurning(Metal.Zinc) || playerReserves.IsBurning(Metal.Brass);
            if (core.gameObject.activeSelf != visible) core.gameObject.SetActive(visible);
            if (!visible) return;

            // 心跳：越生氣跳越快；每次跳動用 |sin|^8 做出「咚」的尖峰
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(2.5f, 7f, anger))), 8f);
            core.localScale = Vector3.one * (1f + 0.5f * beat + 0.4f * anger);

            var c = Color.Lerp(Calm, Angry, anger);

            // 透明度：近 50% → 遠 25%（35 公尺以外都是 25%）
            float dist = Vector3.Distance(player.position, core.position);
            c.a = Mathf.Lerp(0.5f, 0.25f, Mathf.Clamp01(dist / 35f));
            coreRenderer.sharedMaterial.color = c;

            // 三顆光點繞著核心轉
            for (int i = 0; i < orbiters.Count; i++)
            {
                float a = Time.time * Mathf.Lerp(2f, 6f, anger) + i * 2.094f;   // 2.094 = 120 度
                orbiters[i].localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a * 1.3f) * 0.4f, Mathf.Sin(a)) * 0.22f;
            }
        }
    }
}
