using UnityEngine;
using UnityEngine.AI;

namespace MP3_Save
{
    /// 龍的遊走 AI：Idle（站著）→ Wander（走到隨機點）或 Sniff（低頭嗅）→ Idle ...
    /// 動畫參數（Dragon.controller）：Speed (float)、Sniff (bool)、AnimSpeed (float)
    [RequireComponent(typeof(NavMeshAgent))]
    public class DragonWander : MonoBehaviour
    {
        public enum AIState { Idle, Wander, Sniff }

        [Header("身分（每隻都要不同）")]
        [SerializeField] string id = "dragon_01";

        [Header("移動")]
        [SerializeField] float moveSpeed = 1.0f;        // 兩隻設不同，證明速度有被存回來
        [SerializeField] float wanderRadius = 12f;      // 以出生點為中心的遊走範圍
        [SerializeField] Vector2 idleTime = new Vector2(1.5f, 4f);
        [SerializeField, Range(0, 1)] float sniffChance = 0.3f;
        [SerializeField] Vector2 sniffTime = new Vector2(3f, 6f);
        [SerializeField] float giveUpAfter = 15f;       // 走太久到不了就放棄，回到 Idle

        [Header("動畫")]
        [SerializeField] float walkAnimRefSpeed = 0.8f; // 走路動畫以 1 倍速播放時對應的移動速度

        NavMeshAgent agent;
        Animator animator;
        Vector3 home;

        AIState state = AIState.Idle;
        float stateTimer;          // Idle / Sniff：剩餘秒數；Wander：已經走了幾秒
        Vector3 wanderTarget;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int SniffHash = Animator.StringToHash("Sniff");
        static readonly int AnimSpeedHash = Animator.StringToHash("AnimSpeed");

        // ---- 給存讀檔用 ----
        public string Id => id;
        public float MoveSpeed => moveSpeed;
        public AIState State => state;
        public float StateTimer => stateTimer;
        public Vector3 WanderTarget => wanderTarget;
        public float CurrentSpeed => agent.velocity.magnitude;
        public string CurrentAction =>
            state == AIState.Sniff ? "Sniff" :
            agent.velocity.magnitude > 0.1f ? "Walk" : "Idle";

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
            agent.speed = moveSpeed;
            home = transform.position;
            stateTimer = Random.Range(idleTime.x, idleTime.y);
        }

        void Update()
        {
            switch (state)
            {
                case AIState.Idle:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        if (Random.value < sniffChance) EnterSniff();
                        else EnterWander();
                    }
                    break;

                case AIState.Wander:
                    stateTimer += Time.deltaTime;
                    bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
                    if (arrived || stateTimer > giveUpAfter) EnterIdle();
                    break;

                case AIState.Sniff:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f) EnterIdle();
                    break;
            }
            UpdateAnimator();
        }

        void EnterIdle()
        {
            state = AIState.Idle;
            stateTimer = Random.Range(idleTime.x, idleTime.y);
            agent.ResetPath();
        }

        void EnterSniff()
        {
            state = AIState.Sniff;
            stateTimer = Random.Range(sniffTime.x, sniffTime.y);
            agent.ResetPath();
        }

        void EnterWander()
        {
            // 在出生點附近隨機挑一點，投影到 NavMesh 上
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = home + Random.insideUnitSphere * wanderRadius;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    state = AIState.Wander;
                    stateTimer = 0f;
                    wanderTarget = hit.position;
                    agent.SetDestination(wanderTarget);
                    return;
                }
            }
            EnterIdle();   // 找不到可走的點，先站著
        }

        void UpdateAnimator()
        {
            if (animator == null) return;
            float v = agent.velocity.magnitude;
            animator.SetFloat(SpeedHash, v);
            animator.SetBool(SniffHash, state == AIState.Sniff);
            animator.SetFloat(AnimSpeedHash, Mathf.Clamp(v / walkAnimRefSpeed, 0.5f, 2.5f));
        }

        /// 讀檔時呼叫：還原 AI 狀態、速度、目標點。位置由 CreatureSaveable 先用 Warp 設好。
        public void RestoreAI(AIState s, float timer, Vector3 target, float savedMoveSpeed, float currentSpeed)
        {
            moveSpeed = savedMoveSpeed;
            agent.speed = moveSpeed;
            state = s;
            stateTimer = timer;
            wanderTarget = target;

            if (state == AIState.Wander)
            {
                agent.SetDestination(wanderTarget);
                agent.velocity = transform.forward * currentSpeed;   // 接著原本的速度走，不從 0 起步
            }
            else
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            if (animator != null)
            {
                animator.SetFloat(SpeedHash, currentSpeed);
                animator.SetBool(SniffHash, state == AIState.Sniff);
                animator.SetFloat(AnimSpeedHash, Mathf.Clamp(currentSpeed / walkAnimRefSpeed, 0.5f, 2.5f));
            }
        }
    }
}