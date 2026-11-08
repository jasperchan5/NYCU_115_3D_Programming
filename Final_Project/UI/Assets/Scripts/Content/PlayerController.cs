using UnityEngine;
using UnityEngine.InputSystem;

namespace FunctionSelection
{
    /// 
    /// 玩家控制：WASD / 方向鍵移動（相對鏡頭方向），Shift 跑步，Space 跳躍。
    /// 速度與跳躍高度來自選到的角色（CharacterDef），所以不同角色手感不同。
    /// 鎔金術（AllomancySession 接上）：
    ///   speedMultiplier：燒白鑞時 × 1.4
    ///   faceCamera     ：瞄準時角色轉向鏡頭前方
    ///   AddImpulse     ：鋼推 / 鐵拉把玩家推開、拉過去，在空中幾乎不減速，才能飛出去
    ///   Launch         ：拉有落腳點的固定物時，沿算好的拋物線飛過去
    /// 
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public CharacterDef def;
        public CharacterRig rig;
        public CameraDirector cam;
        public float boundsRadius = 60f;
        public bool inputEnabled = true;
        public float speedMultiplier = 1f;
        public bool faceCamera;

        public const float Gravity = 22f;
        CharacterController cc;
        Vector3 planar;
        Vector3 push;                              // 鋼推 / 鐵拉給的水平速度（和走路速度分開，減速方式不同）
        bool launched;                             // 被「發射」出去、還沒落地：不吃鍵盤輸入，水平速度不衰減
        float launchTime;
        float vy;

        public float Speed01 => def == null ? 0f : planar.magnitude / def.runSpeed;
        public bool IsGrounded => cc.isGrounded;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0, 1f, 0);
            cc.stepOffset = 0.35f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var kb = Keyboard.current;

            Vector2 input = Vector2.zero;
            bool run = false, jump = false;
            if (launched)
            {
                // 沿拋物線飛行中：落地（起飛 0.15 秒後碰到地面）才恢復控制
                launchTime += dt;
                if (launchTime > 0.15f && cc.isGrounded) launched = false;
            }
            if (inputEnabled && !launched && kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;
                run = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }

            // 移動方向以鏡頭的水平朝向為準
            var yawRot = Quaternion.Euler(0, cam != null ? cam.Yaw : transform.eulerAngles.y, 0);
            var dir = yawRot * new Vector3(input.x, 0, input.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            float targetSpeed = (run ? def.runSpeed : def.walkSpeed) * speedMultiplier;
            float accel = cc.isGrounded ? 30f : 8f;
            planar = Vector3.MoveTowards(planar, dir * targetSpeed, accel * dt);

            if (cc.isGrounded)
            {
                if (vy < 0f) vy = -2f;
                if (jump) vy = Mathf.Sqrt(2f * Gravity * def.jumpHeight);
            }
            vy -= Gravity * dt;

            cc.Move((planar + push + Vector3.up * vy) * dt);
            // 推拉的速度：在地上很快被摩擦力停下，在空中幾乎不減（才能飛出去）
            if (!launched) push = Vector3.MoveTowards(push, Vector3.zero, (cc.isGrounded ? 25f : 2f) * dt);

            // 不能走出圍籬
            var flat = new Vector3(transform.position.x, 0, transform.position.z);
            if (flat.magnitude > boundsRadius)
            {
                var clamped = flat.normalized * boundsRadius;
                cc.enabled = false;
                transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
                cc.enabled = true;
            }

            // 平滑轉向移動方向；瞄準時轉向鏡頭前方
            if (faceCamera || dir.sqrMagnitude > 0.01f)
            {
                var want = faceCamera ? yawRot : Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-12f * dt));
            }

            rig.SetLocomotion(Speed01, cc.isGrounded);
        }

        /// 被鋼推 / 鐵拉：水平部分加到 push，垂直部分加到 vy（可以被推上天）。
        public void AddImpulse(Vector3 v)
        {
            push += new Vector3(v.x, 0f, v.z);
            vy = Mathf.Max(vy, 0f) + v.y;
        }

        /// 
        /// 沿拋物線發射（拉有落腳點的固定物時）：直接「設定」速度，而且飛行中不吃鍵盤、水平速度不衰減，
        /// 這樣算好的拋物線才會剛好落在落腳點。
        /// 
        public void Launch(Vector3 v)
        {
            planar = Vector3.zero;
            push = new Vector3(v.x, 0f, v.z);
            vy = v.y;
            launched = true;
            launchTime = 0f;
        }

        public void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            planar = push = Vector3.zero;
            launched = false;
            vy = 0f;
        }
    }
}
