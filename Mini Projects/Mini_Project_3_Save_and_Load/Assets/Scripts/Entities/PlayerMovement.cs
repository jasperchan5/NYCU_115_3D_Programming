using UnityEngine;
using UnityEngine.InputSystem;

namespace MP3_Save
{
    /// 第三人稱移動：WASD 移動（相對鏡頭方向）、Shift 跑、Space 跳。
    /// 動畫參數：Speed (float)、Grounded (bool)、Jump (trigger)。
    /// 對外開放 CurrentSpeed / VerticalVelocity / CurrentAction，給 PlayerSaveable 存讀檔用。
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("速度")]
        [SerializeField] float walkSpeed = 2f;
        [SerializeField] float runSpeed = 5f;
        [SerializeField] float acceleration = 10f;   // 每秒能加減多少速度
        [SerializeField] float turnSpeed = 720f;     // 每秒最多轉幾度

        [Header("跳躍")]
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float gravity = -20f;

        [Header("鏡頭（留空就用 Main Camera）")]
        [SerializeField] Transform cameraTransform;

        CharacterController controller;
        Animator animator;
        float currentSpeed;       // 目前水平速度
        float verticalVelocity;   // 目前垂直速度（跳躍、下落）

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int JumpHash = Animator.StringToHash("Jump");

        // ---- 給存讀檔用 ----
        public float CurrentSpeed => currentSpeed;
        public float VerticalVelocity => verticalVelocity;
        public bool IsGrounded => controller.isGrounded;
        public string CurrentAction
        {
            get
            {
                if (!controller.isGrounded) return "Jump";
                if (currentSpeed > (walkSpeed + runSpeed) * 0.5f) return "Run";
                if (currentSpeed > 0.1f) return "Walk";
                return "Idle";
            }
        }

        /// 讀檔時呼叫：直接設定速度，動畫參數也立刻套用（不經過平滑）。
        public void RestoreMotion(float speed, float vertical, bool grounded)
        {
            currentSpeed = speed;
            verticalVelocity = vertical;
            if (animator == null) return;
            animator.ResetTrigger(JumpHash);          // 清掉存檔前殘留的跳躍指令
            animator.SetFloat(SpeedHash, speed);
            animator.SetBool(GroundedHash, grounded);
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;   // 位移完全交給 CharacterController
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            // 1. 讀鍵盤
            Vector2 input = Vector2.zero;
            bool run = false, jump = false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
                run = kb.leftShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }
            input = Vector2.ClampMagnitude(input, 1f);

            // 2. 把輸入換成世界方向（以鏡頭的前方為準，壓到水平面）
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            if (cameraTransform != null)
            {
                fwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }
            Vector3 dir = fwd * input.y + right * input.x;
            bool hasInput = dir.sqrMagnitude > 0.01f;

            // 3. 速度平滑加減、轉向
            float target = hasInput ? (run ? runSpeed : walkSpeed) * input.magnitude : 0f;
            currentSpeed = Mathf.MoveTowards(currentSpeed, target, acceleration * Time.deltaTime);
            if (hasInput)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }

            // 4. 重力與跳躍
            bool grounded = controller.isGrounded;
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;   // 貼地，避免 isGrounded 閃爍
            if (grounded && jump)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                if (animator != null) animator.SetTrigger(JumpHash);
            }
            verticalVelocity += gravity * Time.deltaTime;

            // 5. 移動
            Vector3 velocity = transform.forward * currentSpeed + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            // 6. 動畫參數
            if (animator != null)
            {
                animator.SetFloat(SpeedHash, currentSpeed, 0.1f, Time.deltaTime);
                animator.SetBool(GroundedHash, controller.isGrounded);
            }
        }
    }
}