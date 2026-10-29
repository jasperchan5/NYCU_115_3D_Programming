using UnityEngine;
using UnityEngine.InputSystem;

namespace Allomancy
{
    /// 
    /// 披著斗篷的玩家（從背後看，像草圖中間那個角色）。
    /// WASD 移動（相對鏡頭方向）、Shift 跑、Space 跳；燃燒白鑞時跑得更快。
    /// 鏡頭：第三人稱，滑鼠控制左右 / 上下；開著金屬輪盤時滑鼠交給輪盤，不轉鏡頭。
    /// 瞄準（按住右鍵）時鏡頭拉近、視角變窄，角色轉向鏡頭前方。
    /// 鋼推 / 鐵拉比自己重的金屬時，玩家會被推開 / 拉過去（AddImpulse）。
    /// 
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public MetalReserves reserves;
        public MetalSelector selector;
        public Camera cam;
        public Transform Chest { get; private set; }
        public bool aiming;                        // 由 AllomanticTargeting 設定
        public float Yaw => yaw;

        CharacterController cc;
        public bool IsGrounded => cc.isGrounded;
        
        Transform cloak;
        float yaw, pitch = 12f, vy, sway;
        Vector3 planarVelocity;
        Vector3 pushVelocity;                      // 被推拉產生的水平速度（和走路速度分開，才不會被走路蓋掉）
        float zoom;                                // 0 = 一般鏡頭，1 = 瞄準鏡頭

        const float WalkSpeed = 3.5f, RunSpeed = 7f, JumpHeight = 1.3f, Gravity = 20f, MouseSensitivity = 0.12f;

        public static PlayerController Create(Transform parent, Vector3 pos, Camera cam)
        {
            var root = new GameObject("Player").transform;
            root.SetParent(parent, false);
            root.position = pos;
            var p = root.gameObject.AddComponent<PlayerController>();
            p.cam = cam;
            p.cc = root.GetComponent<CharacterController>();
            p.cc.height = 1.9f;
            p.cc.radius = 0.4f;
            p.cc.center = new Vector3(0, 0.95f, 0);

            // 斗篷：上窄下寬（膠囊 + 往下張開的方塊），頭套是一顆球
            var dark = Build.Mat(new Color(0.12f, 0.12f, 0.15f));
            p.cloak = new GameObject("Cloak").transform;
            p.cloak.SetParent(root, false);
            Build.Prim(PrimitiveType.Capsule, p.cloak, new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.75f, 0.45f), dark);
            Build.Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.78f, 0), new Vector3(0.42f, 0.46f, 0.44f), dark, name: "Hood");

            // 胸口：鋼線的起點
            p.Chest = new GameObject("Chest").transform;
            p.Chest.SetParent(root, false);
            p.Chest.localPosition = new Vector3(0, 1.35f, 0.25f);
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            bool selecting = selector != null && selector.IsOpen;

            // ---- 滑鼠轉鏡頭 ----
            if (!selecting && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue();
                yaw += d.x * MouseSensitivity;
                pitch = Mathf.Clamp(pitch - d.y * MouseSensitivity, -65f, 60f);
            }

            // ---- 移動 ----
            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1;
            if (kb.sKey.isPressed) input.y -= 1;
            if (kb.dKey.isPressed) input.x += 1;
            if (kb.aKey.isPressed) input.x -= 1;
            var dir = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            float speed = kb.leftShiftKey.isPressed ? RunSpeed : WalkSpeed;
            if (reserves.IsBurning(Metal.Pewter)) speed *= 1.4f;        // 白鑞：身體強化
            planarVelocity = Vector3.MoveTowards(planarVelocity, dir * speed, 30f * dt);

            if (cc.isGrounded)
            {
                if (vy < 0f) vy = -2f;
                if (kb.spaceKey.wasPressedThisFrame) vy = Mathf.Sqrt(2f * Gravity * JumpHeight);
            }
            vy -= Gravity * dt;
            cc.Move((planarVelocity + pushVelocity + Vector3.up * vy) * dt);
            // 推拉的速度：在地上很快被摩擦力停下，在空中幾乎不減（才能飛出去）
            pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, (cc.isGrounded ? 25f : 2f) * dt);

            // 角色面向移動方向；停下時面向鏡頭前方
            var face = dir.sqrMagnitude > 0.01f && !aiming ? dir : Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-10f * dt));

            // 斗篷隨速度前後擺動
            sway += dt * planarVelocity.magnitude * 2.5f;
            cloak.localRotation = Quaternion.Euler(-planarVelocity.magnitude * 1.5f + Mathf.Sin(sway) * 3f, 0, Mathf.Cos(sway) * 2f);

            // 點一下畫面就鎖定滑鼠
            if (mouse.leftButton.wasPressedThisFrame && !selecting) Cursor.lockState = CursorLockMode.Locked;
            if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
        }

        void LateUpdate()
        {
            // 第三人稱鏡頭：在角色後上方，看向頭部；瞄準時拉近到 2.6 公尺、更偏右、視角從 60° 縮到 45°
            zoom = Mathf.MoveTowards(zoom, aiming ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            float side = Mathf.Lerp(0.6f, 0.85f, zoom);
            var pivot = transform.position + Vector3.up * 1.7f;
            var offset = Quaternion.Euler(pitch, yaw, 0) * new Vector3(side, 0.3f, -Mathf.Lerp(4.5f, 2.6f, zoom));   // 越肩視角
            var camPos = pivot + offset;
            camPos.y = Mathf.Max(camPos.y, transform.position.y + 0.3f);   // 最低只到腳底上方 30 cm，不會鑽進地板
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        /// 被鋼推 / 鐵拉：水平部分加到 pushVelocity，垂直部分加到 vy（可以被推上天）。
        public void AddImpulse(Vector3 v)
        {
            pushVelocity += new Vector3(v.x, 0f, v.z);
            vy = Mathf.Max(vy, 0f) + v.y;
        }

        public void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
            vy = 0f;
            planarVelocity = pushVelocity = Vector3.zero;
        }
    }
}
