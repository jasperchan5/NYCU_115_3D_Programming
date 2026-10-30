using UnityEngine;
using UnityEngine.InputSystem;

namespace FunctionSelection
{
    /// 
    /// 玩家控制：WASD / 方向鍵移動（相對鏡頭方向），Shift 跑步，Space 跳躍。
    /// 速度與跳躍高度來自選到的角色（CharacterDef），所以不同角色手感不同。
    /// 
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public CharacterDef def;
        public CharacterRig rig;
        public CameraDirector cam;
        public float boundsRadius = 60f;
        public bool inputEnabled = true;

        const float Gravity = 22f;
        CharacterController cc;
        Vector3 planar, knock;
        float vy;

        public float Speed01 => def == null ? 0f : planar.magnitude / def.runSpeed;

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
            if (inputEnabled && kb != null)
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

            float targetSpeed = run ? def.runSpeed : def.walkSpeed;
            float accel = cc.isGrounded ? 30f : 8f;
            planar = Vector3.MoveTowards(planar, dir * targetSpeed, accel * dt);

            if (cc.isGrounded)
            {
                if (vy < 0f) vy = -2f;
                if (jump) vy = Mathf.Sqrt(2f * Gravity * def.jumpHeight);
            }
            vy -= Gravity * dt;

            knock = Vector3.MoveTowards(knock, Vector3.zero, 18f * dt);
            cc.Move((planar + knock + Vector3.up * vy) * dt);

            // 不能走出圍籬
            var flat = new Vector3(transform.position.x, 0, transform.position.z);
            if (flat.magnitude > boundsRadius)
            {
                var clamped = flat.normalized * boundsRadius;
                cc.enabled = false;
                transform.position = new Vector3(clamped.x, transform.position.y, clamped.z);
                cc.enabled = true;
            }

            // 平滑轉向移動方向
            if (dir.sqrMagnitude > 0.01f)
            {
                var want = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-12f * dt));
            }

            rig.SetLocomotion(Speed01, cc.isGrounded);
        }

        /// 被怪物撞到時往後彈。
        public void Knockback(Vector3 from, float force)
        {
            var d = transform.position - from;
            d.y = 0f;
            knock = d.normalized * force;
            vy = 6f;
        }

        public void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            planar = knock = Vector3.zero;
            vy = 0f;
        }
    }
}
